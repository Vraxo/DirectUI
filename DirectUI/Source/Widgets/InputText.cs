// Widgets/LineEdit.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using DirectUI.Core;
using Vortice.Mathematics;
using D2D = Vortice.Direct2D1; // Still used for AntialiasMode enum
using Vortice.DirectWrite; // Still used for WordWrapping, TextAlignment, ParagraphAlignment

namespace DirectUI;

// Logic and drawing for an immediate-mode LineEdit control. This class is STATELESS.
// All persistent state is managed in a separate LineEditState object.
internal class InputText
{
    // === MAIN UPDATE && DRAW METHOD ===
    public bool UpdateAndDraw(
        int intId,
        ref string text,
        InputTextState state, // The state is now passed in
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
        var renderer = context.Renderer;
        var textService = context.TextService;
        var input = context.InputState;

        var themeId = HashCode.Combine(intId, "theme");
        var finalTheme = theme ?? uiState.GetOrCreateElement<ButtonStylePack>(themeId);

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

        // --- Focus Management && Caret Placement on Click ---
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

            // Use ITextService to get the text layout
            var textLayout = textService.GetTextLayout(textForHitTest, styleForHitTest, new(float.MaxValue, size.Y), new Alignment(HAlignment.Left, VAlignment.Center));
            if (textLayout is not null)
            {
                float relativeClickX = input.MousePosition.X - contentRectForHitTest.Left + state.ScrollPixelOffset;
                float relativeClickY = input.MousePosition.Y - contentRectForHitTest.Top;

                var hitTestResult = textLayout.HitTestPoint(new Vector2(relativeClickX, relativeClickY));

                int newCaretPos = hitTestResult.TextPosition;
                if (hitTestResult.IsTrailingHit)
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
        renderer.DrawBox(bounds, finalTheme.Current);

        // Define content area and clip to it
        Rect contentRect = new Rect(
            bounds.X + textMargin.X,
            bounds.Y + textMargin.Y,
            Math.Max(0, bounds.Width - textMargin.X * 2),
            Math.Max(0, bounds.Height - textMargin.Y * 2)
        );
        renderer.PushClipRect(contentRect, D2D.AntialiasMode.PerPrimitive);

        string textToDraw;
        ButtonStyle styleToDraw;

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

        DrawVisibleText(textToDraw, state, size, styleToDraw, contentRect.TopLeft);

        if (uiState.FocusedElementId == intId && state.IsBlinkOn)
        {
            DrawCaret(textToDraw, state, size, finalTheme.Current, contentRect);
        }

        renderer.PopClipRect();

        return textChanged;
    }

    private bool ProcessInput(ref string text, InputTextState state, Vector2 size, int maxLength, Vector2 textMargin, InputState input)
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

    private void DrawVisibleText(string fullText, InputTextState state, Vector2 size, ButtonStyle style, Vector2 contentTopLeft)
    {
        if (string.IsNullOrEmpty(fullText))
        {
            return;
        }

        var context = UI.Context;
        var renderer = context.Renderer;
        var textService = context.TextService;

        // The LineEdit widget uses ITextLayout for hit-testing and caret position calculations.
        // Even though IRenderer now takes raw text for drawing, ITextLayout is still relevant for widget logic.
        var textLayout = textService.GetTextLayout(fullText, style, new(float.MaxValue, size.Y), new Alignment(HAlignment.Left, VAlignment.Center));

        if (textLayout is null) return;

        // A small vertical adjustment to compensate for font metrics making text appear slightly too low when using ParagraphAlignment.Center.
        const float yOffsetCorrection = -1.5f;

        // Calculate the drawing origin by applying scroll offset directly.
        Vector2 drawOrigin = new Vector2(contentTopLeft.X - state.ScrollPixelOffset, contentTopLeft.Y + yOffsetCorrection);

        // Renderer's DrawText method now takes full text parameters.
        renderer.DrawText(drawOrigin, fullText, style, new Alignment(HAlignment.Left, VAlignment.Center), new Vector2(float.MaxValue, size.Y), style.FontColor);
    }

    private void DrawCaret(string text, InputTextState state, Vector2 size, ButtonStyle style, Rect contentRect)
    {
        var textService = UI.Context.TextService;
        var renderer = UI.Context.Renderer;

        // Use ITextService to get the text layout
        var textLayout = textService.GetTextLayout(text, style, new(float.MaxValue, size.Y), new Alignment(HAlignment.Left, VAlignment.Center));
        if (textLayout is null) return;

        // HitTestTextPosition on the ITextLayout (DirectWriteTextLayout provides this)
        var hitTestMetrics = textLayout.HitTestTextPosition(state.CaretPosition, false);

        float caretX = contentRect.Left + hitTestMetrics.Point.X - state.ScrollPixelOffset;
        Rect caretRect = new Rect(caretX, contentRect.Top, 1, contentRect.Height);

        renderer.DrawBox(caretRect, new BoxStyle { FillColor = style.FontColor, Roundness = 0f, BorderLength = 0f });
    }

    private ITextLayout? GetTextLayout(string text, Vector2 size, ButtonStyle style, float maxWidth)
    {
        // LineEdit needs to re-layout text based on user input, so it requests a fresh layout from the service.
        // The ITextService handles the caching internally for performance.
        return UI.Context.TextService.GetTextLayout(text, style, new(maxWidth, size.Y), new Alignment(HAlignment.Left, VAlignment.Center));
    }

    private void UpdateView(string text, InputTextState state, Vector2 size, Vector2 textMargin)
    {
        float availableWidth = size.X - textMargin.X * 2;
        var style = new ButtonStyle(); // A default style is fine for measuring

        var textLayout = UI.Context.TextService.GetTextLayout(text, style, new(float.MaxValue, size.Y), new Alignment(HAlignment.Left, VAlignment.Center));
        if (textLayout is null) return;

        var hitTestMetrics = textLayout.HitTestTextPosition(state.CaretPosition, false);
        float caretAbsoluteX = hitTestMetrics.Point.X;

        if (caretAbsoluteX - state.ScrollPixelOffset > availableWidth)
        {
            state.ScrollPixelOffset = caretAbsoluteX - availableWidth;
        }
        else if (caretAbsoluteX - state.ScrollPixelOffset < 0)
        {
            state.ScrollPixelOffset = caretAbsoluteX;
        }

        float maxScroll = Math.Max(0, textLayout.Size.X - availableWidth);
        state.ScrollPixelOffset = Math.Clamp(state.ScrollPixelOffset, 0, maxScroll);
    }

    private static void PushUndoState(string text, InputTextState state)
    {
        var lastState = state.UndoStack.Count > 0 ? state.UndoStack.Peek() : default;
        if (state.UndoStack.Count > 0 && lastState.Text == text && lastState.CaretPosition == state.CaretPosition) return;

        if (state.UndoStack.Count >= InputTextState.HistoryLimit) state.UndoStack.Pop();
        state.UndoStack.Push(new LineEditUndoRecord(text, state.CaretPosition, state.ScrollPixelOffset));
        state.RedoStack.Clear();
    }

    private static void Undo(ref string text, InputTextState state)
    {
        if (state.UndoStack.Count == 0) return;
        state.RedoStack.Push(new LineEditUndoRecord(text, state.CaretPosition, state.ScrollPixelOffset));
        var lastState = state.UndoStack.Pop();
        text = lastState.Text;
        state.CaretPosition = lastState.CaretPosition;
        state.ScrollPixelOffset = lastState.ScrollPixelOffset;
    }

    private static void Redo(ref string text, InputTextState state)
    {
        if (state.RedoStack.Count == 0) return;
        state.UndoStack.Push(new LineEditUndoRecord(text, state.CaretPosition, state.ScrollPixelOffset));
        var nextState = state.RedoStack.Pop();
        text = nextState.Text;
        state.CaretPosition = nextState.CaretPosition;
        state.ScrollPixelOffset = nextState.ScrollPixelOffset;
    }
}