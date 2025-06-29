// Widgets/LineEdit.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;
using D2D = Vortice.Direct2D1;
using SharpGen.Runtime;

namespace DirectUI;

// Logic and drawing for an immediate-mode LineEdit control. This class is STATELESS.
// All persistent state is managed in a separate LineEditState object.
internal class LineEdit
{
    // === CACHED RENDERING RESOURCES (transient, not persistent state) ===
    private IDWriteTextLayout? _cachedTextLayout;
    private string _cachedText = " "; // Use a non-null, non-empty default
    private ButtonStyle? _cachedStyle;
    private float _cachedMaxWidth = -1;

    // === MAIN UPDATE & DRAW METHOD ===
    public bool UpdateAndDraw(
        int intId,
        ref string text,
        LineEditState state, // The state is now passed in
        Vector2 position,
        Vector2 size,
        ButtonStylePack? theme,
        string placeholderText,
        bool isPassword,
        char passwordChar,
        int maxLength,
        bool disabled,
        Vector2 textMargin)
    {
        var context = UI.Context;
        var uiState = UI.State;
        var resources = UI.Resources;

        var themeId = HashCode.Combine(intId, "theme");
        var finalTheme = theme ?? uiState.GetOrCreateElement<ButtonStylePack>(themeId);
        var input = context.InputState;

        // If no specific theme was provided, configure the default one for a LineEdit look.
        if (theme is null)
        {
            // This setup runs once per widget instance and is then cached.
            finalTheme.Roundness = 0f;
            finalTheme.BorderLength = 1f;

            // Normal state (inset look)
            finalTheme.Normal.FillColor = DefaultTheme.NormalBorder; // Very dark, same as window border
            finalTheme.Normal.BorderColor = Colors.Black;

            // Hover state (subtle brightening)
            finalTheme.Hover.FillColor = new Color4(35 / 255f, 35 / 255f, 35 / 255f, 1.0f);
            finalTheme.Hover.BorderColor = Colors.Black;

            // Focused state (bright border)
            finalTheme.Focused.FillColor = finalTheme.Normal.FillColor;
            finalTheme.Focused.BorderColor = DefaultTheme.FocusBorder;

            // Disabled state
            finalTheme.Disabled.FillColor = finalTheme.Normal.FillColor;
            finalTheme.Disabled.BorderColor = new Color4(48 / 255f, 48 / 255f, 48 / 255f, 1.0f);
        }

        Rect bounds = new(position.X, position.Y, size.X, size.Y);
        bool textChanged = false;

        // --- Focus Management & Caret Placement on Click ---
        bool isHovering = bounds.Contains(input.MousePosition.X, input.MousePosition.Y);
        bool isActive = uiState.ActivelyPressedElementId == intId;

        // BUG FIX: Handle losing active press state on mouse up.
        // Without this, the control would lock all other input after being clicked.
        if (isActive && !input.IsLeftMouseDown)
        {
            uiState.ClearActivePress(intId);
        }

        if (input.WasLeftMousePressedThisFrame && isHovering && !disabled)
        {
            uiState.SetFocus(intId);
            uiState.SetPotentialCaptorForFrame(intId); // Claim the press

            // --- BEGIN: Caret positioning logic on click ---
            // We need to temporarily calculate drawing info here for hit-testing.
            // This is the only place we need to do this, as keyboard input moves the caret programmatically.
            finalTheme.UpdateCurrentStyle(isHovering, false, disabled, true); // We are now focused
            var styleForHitTest = finalTheme.Current;
            var textForHitTest = isPassword ? new string(passwordChar, text.Length) : text;
            var contentRectForHitTest = new Rect(
                bounds.X + textMargin.X,
                bounds.Y + textMargin.Y,
                Math.Max(0, bounds.Width - textMargin.X * 2),
                Math.Max(0, bounds.Height - textMargin.Y * 2)
            );

            var textLayout = GetTextLayout(textForHitTest, size, styleForHitTest, float.MaxValue);
            if (textLayout != null)
            {
                float relativeClickX = input.MousePosition.X - contentRectForHitTest.Left + state.ScrollPixelOffset;
                float relativeClickY = input.MousePosition.Y - contentRectForHitTest.Top;

                textLayout.HitTestPoint(relativeClickX, relativeClickY, out RawBool isTrailingHit, out RawBool isInside, out var hitTestMetrics);

                int newCaretPos = (int)hitTestMetrics.TextPosition;
                if (isTrailingHit)
                {
                    newCaretPos++;
                }

                state.CaretPosition = Math.Clamp(newCaretPos, 0, text.Length);

                // Reset blink and update scroll to show caret.
                state.IsBlinkOn = true;
                state.BlinkTimer = 0;
                UpdateView(text, state, size, textMargin);
            }
            // --- END: Caret positioning logic on click ---
        }

        // --- Input Processing ---
        if (uiState.FocusedElementId == intId && !disabled)
        {
            textChanged = ProcessInput(ref text, state, size, maxLength, textMargin, input);
        }

        // --- Drawing ---
        finalTheme.UpdateCurrentStyle(isHovering, false, disabled, uiState.FocusedElementId == intId);
        resources.DrawBoxStyleHelper(context.RenderTarget, position, size, finalTheme.Current);

        // Define content area and clip to it
        Rect contentRect = new Rect(
            bounds.X + textMargin.X,
            bounds.Y + textMargin.Y,
            Math.Max(0, bounds.Width - textMargin.X * 2),
            Math.Max(0, bounds.Height - textMargin.Y * 2)
        );
        context.RenderTarget.PushAxisAlignedClip(contentRect, D2D.AntialiasMode.PerPrimitive);

        string textToDraw;
        ButtonStyle styleToDraw;
        Vector2 drawPos = contentRect.TopLeft;

        if (string.IsNullOrEmpty(text) && !(uiState.FocusedElementId == intId))
        {
            textToDraw = placeholderText;
            styleToDraw = new ButtonStyle(finalTheme.Disabled)
            {
                FontColor = new Color4(100 / 255f, 100 / 255f, 100 / 255f, 1.0f) // Custom placeholder color
            };
        }
        else
        {
            textToDraw = isPassword ? new string(passwordChar, text.Length) : text;
            styleToDraw = finalTheme.Current;
        }

        DrawVisibleText(textToDraw, state, size, styleToDraw, drawPos);

        if (uiState.FocusedElementId == intId && state.IsBlinkOn)
        {
            DrawCaret(textToDraw, state, size, finalTheme.Current, contentRect);
        }

        context.RenderTarget.PopAxisAlignedClip();

        return textChanged;
    }

    private bool ProcessInput(ref string text, LineEditState state, Vector2 size, int maxLength, Vector2 textMargin, InputState input)
    {
        bool textChanged = false;

        state.BlinkTimer += UI.Context.DeltaTime;
        
        if (state.BlinkTimer > 0.5f)
        {
            state.BlinkTimer = 0;
            state.IsBlinkOn = !state.IsBlinkOn;
        }

        bool isCtrlHeld = input.HeldKeys.Contains(Keys.Control);

        if (input.TypedCharacters.Any())
        {
            PushUndoState(text, state);
            
            foreach (char c in input.TypedCharacters)
            {
                if (text.Length >= maxLength)
                {
                    continue;
                }

                text = text.Insert(state.CaretPosition, c.ToString());
                state.CaretPosition++;
                textChanged = true;
            }
        }

        foreach (var key in input.PressedKeys)
        {
            bool hasChanged = true;
            PushUndoState(text, state); // Push on first key press
            switch (key)
            {
                case Keys.Backspace:
                    if (state.CaretPosition > 0)
                    {
                        int removeCount = 1;
                        if (isCtrlHeld) removeCount = state.CaretPosition - FindPreviousWordStart(text, state.CaretPosition);
                        text = text.Remove(state.CaretPosition - removeCount, removeCount);
                        state.CaretPosition -= removeCount;
                    }
                    break;
                case Keys.Delete:
                    if (state.CaretPosition < text.Length)
                    {
                        int removeCount = 1;
                        if (isCtrlHeld) removeCount = FindNextWordEnd(text, state.CaretPosition) - state.CaretPosition;
                        text = text.Remove(state.CaretPosition, removeCount);
                    }
                    break;
                case Keys.LeftArrow:
                    state.CaretPosition = isCtrlHeld ? FindPreviousWordStart(text, state.CaretPosition) : state.CaretPosition - 1;
                    break;
                case Keys.RightArrow:
                    state.CaretPosition = isCtrlHeld ? FindNextWordEnd(text, state.CaretPosition) : state.CaretPosition + 1;
                    break;
                case Keys.Home: state.CaretPosition = 0; break;
                case Keys.End: state.CaretPosition = text.Length; break;
                case Keys.Z when isCtrlHeld: Undo(ref text, state); break;
                case Keys.Y when isCtrlHeld: Redo(ref text, state); break;
                default: hasChanged = false; break;
            }
            if (hasChanged) textChanged = true;
        }

        state.CaretPosition = Math.Clamp(state.CaretPosition, 0, text.Length);

        if (textChanged || input.PressedKeys.Any(k => k is Keys.LeftArrow or Keys.RightArrow or Keys.Home or Keys.End))
        {
            UpdateView(text, state, size, textMargin);
            state.IsBlinkOn = true;
            state.BlinkTimer = 0;
        }

        return textChanged;
    }

    private static int FindPreviousWordStart(string text, int currentPos)
    {
        if (currentPos == 0)
        {
            return 0;
        }

        int pos = currentPos - 1;
        while (pos > 0 && char.IsWhiteSpace(text[pos])) pos--;
        while (pos > 0 && !char.IsWhiteSpace(text[pos - 1])) pos--;
        return pos;
    }

    private static int FindNextWordEnd(string text, int currentPos)
    {
        if (currentPos >= text.Length)
        {
            return text.Length;
        }

        int pos = currentPos;
        while (pos < text.Length && !char.IsWhiteSpace(text[pos])) pos++;
        while (pos < text.Length && char.IsWhiteSpace(text[pos])) pos++;
        return pos;
    }

    private void DrawVisibleText(string fullText, LineEditState state, Vector2 size, ButtonStyle style, Vector2 contentTopLeft)
    {
        if (string.IsNullOrEmpty(fullText))
        {
            return;
        }

        var context = UI.Context;
        var rt = context.RenderTarget;
        var textBrush = UI.Resources.GetOrCreateBrush(rt, style.FontColor);
        var textLayout = GetTextLayout(fullText, size, style, float.MaxValue);

        if (textBrush == null || textLayout == null) return;

        var originalTransform = rt.Transform;

        // A small vertical adjustment to compensate for font metrics making text appear slightly too low when using ParagraphAlignment.Center.
        const float yOffsetCorrection = -1.5f;
        var translation = Matrix3x2.CreateTranslation(contentTopLeft.X - state.ScrollPixelOffset, contentTopLeft.Y + yOffsetCorrection);
        rt.Transform = translation * originalTransform;

        rt.DrawTextLayout(Vector2.Zero, textLayout, textBrush, DrawTextOptions.None);

        rt.Transform = originalTransform;
    }

    private void DrawCaret(string text, LineEditState state, Vector2 size, ButtonStyle style, Rect contentRect)
    {
        var textLayout = GetTextLayout(text, size, style, float.MaxValue);
        if (textLayout == null) return;

        textLayout.HitTestTextPosition((uint)state.CaretPosition, false, out _, out _, out var hitTestMetrics);
        float caretX = contentRect.Left + hitTestMetrics.Left - state.ScrollPixelOffset;
        Rect caretRect = new Rect(caretX, contentRect.Top, 1, contentRect.Height);

        var context = UI.Context;
        var caretBrush = UI.Resources.GetOrCreateBrush(context.RenderTarget, style.FontColor);
        if (caretBrush != null)
        {
            context.RenderTarget.FillRectangle(caretRect, caretBrush);
        }
    }

    private IDWriteTextLayout? GetTextLayout(string text, Vector2 size, ButtonStyle style, float maxWidth)
    {
        var dwrite = UI.Context.DWriteFactory;

        // Check if cached layout is still valid
        if (_cachedTextLayout != null && _cachedText == text && _cachedStyle == style && _cachedMaxWidth == maxWidth)
        {
            return _cachedTextLayout;
        }

        // If not, create a new one
        var textFormat = UI.Resources.GetOrCreateTextFormat(dwrite, style);
        if (textFormat == null) return null;

        textFormat.WordWrapping = WordWrapping.NoWrap;
        textFormat.TextAlignment = Vortice.DirectWrite.TextAlignment.Leading;
        textFormat.ParagraphAlignment = ParagraphAlignment.Center;

        _cachedTextLayout?.Dispose();
        _cachedTextLayout = dwrite.CreateTextLayout(text, textFormat, maxWidth, size.Y);

        // Update cache state
        _cachedText = text;
        _cachedStyle = style;
        _cachedMaxWidth = maxWidth;

        return _cachedTextLayout;
    }

    private void UpdateView(string text, LineEditState state, Vector2 size, Vector2 textMargin)
    {
        float availableWidth = size.X - textMargin.X * 2;
        var style = new ButtonStyle(); // A default style is fine for measuring

        var textLayout = GetTextLayout(text, size, style, float.MaxValue);
        if (textLayout == null) return;

        textLayout.HitTestTextPosition((uint)state.CaretPosition, false, out _, out _, out var hitTestMetrics);
        float caretAbsoluteX = hitTestMetrics.Left;

        if (caretAbsoluteX - state.ScrollPixelOffset > availableWidth)
        {
            state.ScrollPixelOffset = caretAbsoluteX - availableWidth;
        }
        else if (caretAbsoluteX - state.ScrollPixelOffset < 0)
        {
            state.ScrollPixelOffset = caretAbsoluteX;
        }

        float maxScroll = Math.Max(0, textLayout.Metrics.WidthIncludingTrailingWhitespace - availableWidth);
        state.ScrollPixelOffset = Math.Clamp(state.ScrollPixelOffset, 0, maxScroll);
    }

    private static void PushUndoState(string text, LineEditState state)
    {
        var lastState = state.UndoStack.Count > 0 ? state.UndoStack.Peek() : default;
        if (state.UndoStack.Count > 0 && lastState.Text == text && lastState.CaretPosition == state.CaretPosition) return;

        if (state.UndoStack.Count >= LineEditState.HistoryLimit) state.UndoStack.Pop();
        state.UndoStack.Push(new LineEditUndoRecord(text, state.CaretPosition, state.ScrollPixelOffset));
        state.RedoStack.Clear();
    }

    private static void Undo(ref string text, LineEditState state)
    {
        if (state.UndoStack.Count == 0) return;
        state.RedoStack.Push(new LineEditUndoRecord(text, state.CaretPosition, state.ScrollPixelOffset));
        var lastState = state.UndoStack.Pop();
        text = lastState.Text;
        state.CaretPosition = lastState.CaretPosition;
        state.ScrollPixelOffset = lastState.ScrollPixelOffset;
    }

    private static void Redo(ref string text, LineEditState state)
    {
        if (state.RedoStack.Count == 0) return;
        state.UndoStack.Push(new LineEditUndoRecord(text, state.CaretPosition, state.ScrollPixelOffset));
        var nextState = state.RedoStack.Pop();
        text = nextState.Text;
        state.CaretPosition = nextState.CaretPosition;
        state.ScrollPixelOffset = nextState.ScrollPixelOffset;
    }
}