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
            finalTheme.Roundness = 0.2f; // Softer corners
            finalTheme.BorderLength = 1f;

            // Normal state (dark, inset look)
            finalTheme.Normal.FillColor = new Color4(30 / 255f, 30 / 255f, 30 / 255f, 1.0f); // Even darker than controls
            finalTheme.Normal.BorderColor = DefaultTheme.NormalBorder;

            // Hover state (subtle border highlight)
            finalTheme.Hover.FillColor = finalTheme.Normal.FillColor;
            finalTheme.Hover.BorderColor = DefaultTheme.HoverBorder;

            // Focused state (bright border)
            finalTheme.Focused.FillColor = finalTheme.Normal.FillColor;
            finalTheme.Focused.BorderColor = DefaultTheme.FocusBorder;

            // Disabled state
            finalTheme.Disabled.FillColor = finalTheme.Normal.FillColor;
            finalTheme.Disabled.BorderColor = DefaultTheme.DisabledBorder;
        }

        Rect bounds = new(position.X, position.Y, size.X, size.Y);
        bool textChanged = false;

        bool isHovering = bounds.Contains(input.MousePosition.X, input.MousePosition.Y);
        if (isHovering)
        {
            uiState.SetPotentialInputTarget(intId);
        }

        bool isPressed = uiState.ActivelyPressedElementId == intId;
        bool isFocused = uiState.FocusedElementId == intId;

        finalTheme.UpdateCurrentStyle(isHovering, isPressed, disabled, isFocused);
        var currentStyle = finalTheme.Current;

        // --- Mouse Input for Selection and Caret ---
        // Handle mouse dragging to select text
        if (isPressed && input.IsLeftMouseDown && input.MousePosition != input.PreviousMousePosition)
        {
            int newCaretPos = GetCaretPositionFromMouse(input.MousePosition, text, state, isPassword, passwordChar, currentStyle, bounds, textMargin, textService);
            if (newCaretPos != state.CaretPosition)
            {
                state.CaretPosition = newCaretPos;
                UpdateView(text, state, size, textMargin);
            }
        }
        // Handle initial mouse press
        else if (input.WasLeftMousePressedThisFrame && isHovering && !disabled && uiState.PotentialInputTargetId == intId)
        {
            if (uiState.TrySetActivePress(intId, 1))
            {
                uiState.SetFocus(intId);

                int newCaretPos = GetCaretPositionFromMouse(input.MousePosition, text, state, isPassword, passwordChar, currentStyle, bounds, textMargin, textService);

                bool isShiftHeld = input.HeldKeys.Contains(Keys.Shift);
                if (!isShiftHeld)
                {
                    state.SelectionAnchor = newCaretPos;
                }
                state.CaretPosition = newCaretPos;

                state.IsBlinkOn = true;
                state.BlinkTimer = 0;
                UpdateView(text, state, size, textMargin);
            }
        }

        if (isPressed && !input.IsLeftMouseDown)
        {
            uiState.ClearActivePress(intId);
        }


        // --- Keyboard Input Processing ---
        if (isFocused && !disabled)
        {
            textChanged = ProcessInput(ref text, state, size, maxLength, textMargin, input);
        }

        // --- Drawing ---
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

        if (string.IsNullOrEmpty(text) && !isFocused)
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

        DrawSelectionHighlight(textToDraw, state, styleToDraw, contentRect);
        DrawVisibleText(textToDraw, state, size, styleToDraw, contentRect.TopLeft);

        if (isFocused && state.IsBlinkOn)
        {
            DrawCaret(textToDraw, state, size, finalTheme.Current, contentRect);
        }

        renderer.PopClipRect();

        return textChanged;
    }

    private int GetCaretPositionFromMouse(Vector2 mousePos, string text, InputTextState state, bool isPassword, char passwordChar, ButtonStyle style, Rect widgetBounds, Vector2 textMargin, ITextService textService)
    {
        var textForHitTest = isPassword ? new string(passwordChar, text.Length) : text;
        var contentRectForHitTest = new Rect(
            widgetBounds.X + textMargin.X,
            widgetBounds.Y + textMargin.Y,
            Math.Max(0, widgetBounds.Width - textMargin.X * 2),
            Math.Max(0, widgetBounds.Height - textMargin.Y * 2)
        );

        var textLayout = textService.GetTextLayout(textForHitTest, style, new(float.MaxValue, widgetBounds.Height), new Alignment(HAlignment.Left, VAlignment.Center));
        if (textLayout is null) return state.CaretPosition;

        float relativeClickX = mousePos.X - contentRectForHitTest.Left + state.ScrollPixelOffset;
        float relativeClickY = mousePos.Y - contentRectForHitTest.Top;
        var hitTestResult = textLayout.HitTestPoint(new Vector2(relativeClickX, relativeClickY));

        int newCaretPos = hitTestResult.TextPosition;
        if (hitTestResult.IsTrailingHit) newCaretPos++;
        return Math.Clamp(newCaretPos, 0, text.Length);
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
        bool isShiftHeld = input.HeldKeys.Contains(Keys.Shift);

        if (input.TypedCharacters.Any())
        {
            PushUndoState(text, state);

            if (state.HasSelection)
            {
                text = text.Remove(state.SelectionStart, state.SelectionEnd - state.SelectionStart);
                state.CaretPosition = state.SelectionStart;
                textChanged = true;
            }

            foreach (char c in input.TypedCharacters)
            {
                if (text.Length >= maxLength) continue;
                text = text.Insert(state.CaretPosition, c.ToString());
                state.CaretPosition++;
                textChanged = true;
            }

            if (textChanged)
            {
                state.SelectionAnchor = state.CaretPosition;
            }
        }

        foreach (var key in input.PressedKeys)
        {
            bool hasTextChangedThisKey = false;
            PushUndoState(text, state); // Push on first key press
            switch (key)
            {
                case Keys.A when isCtrlHeld:
                    state.SelectionAnchor = 0;
                    state.CaretPosition = text.Length;
                    break;
                case Keys.Backspace:
                    if (state.HasSelection)
                    {
                        text = text.Remove(state.SelectionStart, state.SelectionEnd - state.SelectionStart);
                        state.CaretPosition = state.SelectionStart;
                        state.SelectionAnchor = state.CaretPosition;
                        hasTextChangedThisKey = true;
                    }
                    else if (state.CaretPosition > 0)
                    {
                        int removeCount = 1;
                        if (isCtrlHeld) removeCount = state.CaretPosition - FindPreviousWordStart(text, state.CaretPosition);
                        text = text.Remove(state.CaretPosition - removeCount, removeCount);
                        state.CaretPosition -= removeCount;
                        state.SelectionAnchor = state.CaretPosition;
                        hasTextChangedThisKey = true;
                    }
                    break;
                case Keys.Delete:
                    if (state.HasSelection)
                    {
                        text = text.Remove(state.SelectionStart, state.SelectionEnd - state.SelectionStart);
                        state.CaretPosition = state.SelectionStart;
                        state.SelectionAnchor = state.CaretPosition;
                        hasTextChangedThisKey = true;
                    }
                    else if (state.CaretPosition < text.Length)
                    {
                        int removeCount = 1;
                        if (isCtrlHeld) removeCount = FindNextWordEnd(text, state.CaretPosition) - state.CaretPosition;
                        text = text.Remove(state.CaretPosition, removeCount);
                        state.SelectionAnchor = state.CaretPosition;
                        hasTextChangedThisKey = true;
                    }
                    break;
                case Keys.LeftArrow:
                    if (isShiftHeld)
                    {
                        var newPos = isCtrlHeld ? FindPreviousWordStart(text, state.CaretPosition) : state.CaretPosition - 1;
                        state.CaretPosition = Math.Max(0, newPos);
                    }
                    else
                    {
                        var newPos = state.HasSelection ? state.SelectionStart : (isCtrlHeld ? FindPreviousWordStart(text, state.CaretPosition) : state.CaretPosition - 1);
                        state.CaretPosition = Math.Max(0, newPos);
                        state.SelectionAnchor = state.CaretPosition;
                    }
                    break;
                case Keys.RightArrow:
                    if (isShiftHeld)
                    {
                        var newPos = isCtrlHeld ? FindNextWordEnd(text, state.CaretPosition) : state.CaretPosition + 1;
                        state.CaretPosition = Math.Min(text.Length, newPos);
                    }
                    else
                    {
                        var newPos = state.HasSelection ? state.SelectionEnd : (isCtrlHeld ? FindNextWordEnd(text, state.CaretPosition) : state.CaretPosition + 1);
                        state.CaretPosition = Math.Min(text.Length, newPos);
                        state.SelectionAnchor = state.CaretPosition;
                    }
                    break;
                case Keys.Home:
                    state.CaretPosition = 0;
                    if (!isShiftHeld) state.SelectionAnchor = state.CaretPosition;
                    break;
                case Keys.End:
                    state.CaretPosition = text.Length;
                    if (!isShiftHeld) state.SelectionAnchor = state.CaretPosition;
                    break;
                case Keys.Z when isCtrlHeld: Undo(ref text, state); hasTextChangedThisKey = true; break;
                case Keys.Y when isCtrlHeld: Redo(ref text, state); hasTextChangedThisKey = true; break;
            }
            if (hasTextChangedThisKey) textChanged = true;
        }

        state.CaretPosition = Math.Clamp(state.CaretPosition, 0, text.Length);

        if (textChanged || input.PressedKeys.Any(k => k is Keys.LeftArrow or Keys.RightArrow or Keys.Home or Keys.End || (k == Keys.A && isCtrlHeld)))
        {
            UpdateView(text, state, size, textMargin);
            state.IsBlinkOn = true;
            state.BlinkTimer = 0;
        }

        return textChanged;
    }

    private static int FindPreviousWordStart(string text, int currentPos)
    {
        if (currentPos == 0) return 0;
        int pos = currentPos - 1;
        while (pos > 0 && char.IsWhiteSpace(text[pos])) pos--;
        while (pos > 0 && !char.IsWhiteSpace(text[pos - 1])) pos--;
        return pos;
    }

    private static int FindNextWordEnd(string text, int currentPos)
    {
        if (currentPos >= text.Length) return text.Length;
        int pos = currentPos;
        while (pos < text.Length && char.IsWhiteSpace(text[pos])) pos++;
        while (pos < text.Length && !char.IsWhiteSpace(text[pos])) pos++;
        while (pos < text.Length && char.IsWhiteSpace(text[pos])) pos++;
        return pos;
    }

    private void DrawSelectionHighlight(string text, InputTextState state, ButtonStyle style, Rect contentRect)
    {
        if (!state.HasSelection) return;

        var textService = UI.Context.TextService;
        var renderer = UI.Context.Renderer;

        var textLayout = textService.GetTextLayout(text, style, new(float.MaxValue, contentRect.Height), new Alignment(HAlignment.Left, VAlignment.Center));
        if (textLayout is null) return;

        var selectionStartMetrics = textLayout.HitTestTextPosition(state.SelectionStart, false);
        var selectionEndMetrics = textLayout.HitTestTextPosition(state.SelectionEnd, false);

        float startX = contentRect.Left + selectionStartMetrics.Point.X - state.ScrollPixelOffset;
        float endX = contentRect.Left + selectionEndMetrics.Point.X - state.ScrollPixelOffset;
        float width = endX - startX;

        if (width <= 0) return;

        Rect highlightRect = new(startX, contentRect.Top, width, contentRect.Height);

        var selectionColor = DefaultTheme.Accent;
        selectionColor.A = 100; // ~40% opacity
        var selectionStyle = new BoxStyle { FillColor = selectionColor, Roundness = 0f, BorderLength = 0f };

        renderer.DrawBox(highlightRect, selectionStyle);
    }

    private void DrawVisibleText(string fullText, InputTextState state, Vector2 size, ButtonStyle style, Vector2 contentTopLeft)
    {
        if (string.IsNullOrEmpty(fullText)) return;

        var context = UI.Context;
        var renderer = context.Renderer;
        var textService = context.TextService;

        var textLayout = textService.GetTextLayout(fullText, style, new(float.MaxValue, size.Y), new Alignment(HAlignment.Left, VAlignment.Center));
        if (textLayout is null) return;

        const float yOffsetCorrection = -1.5f;
        Vector2 drawOrigin = new Vector2(contentTopLeft.X - state.ScrollPixelOffset, contentTopLeft.Y + yOffsetCorrection);
        renderer.DrawText(drawOrigin, fullText, style, new Alignment(HAlignment.Left, VAlignment.Center), new Vector2(float.MaxValue, size.Y), style.FontColor);
    }

    private void DrawCaret(string text, InputTextState state, Vector2 size, ButtonStyle style, Rect contentRect)
    {
        var textService = UI.Context.TextService;
        var renderer = UI.Context.Renderer;

        var textLayout = textService.GetTextLayout(text, style, new(float.MaxValue, size.Y), new Alignment(HAlignment.Left, VAlignment.Center));
        if (textLayout is null) return;

        var hitTestMetrics = textLayout.HitTestTextPosition(state.CaretPosition, false);
        float caretX = contentRect.Left + hitTestMetrics.Point.X - state.ScrollPixelOffset;
        Rect caretRect = new(caretX, contentRect.Top, 1, contentRect.Height);
        renderer.DrawBox(caretRect, new BoxStyle { FillColor = style.FontColor, Roundness = 0f, BorderLength = 0f });
    }

    private void UpdateView(string text, InputTextState state, Vector2 size, Vector2 textMargin)
    {
        float availableWidth = size.X - textMargin.X * 2;
        var style = new ButtonStyle(); // A default style is fine for measuring

        var textLayout = UI.Context.TextService.GetTextLayout(text, style, new(float.MaxValue, size.Y), new Alignment(HAlignment.Left, VAlignment.Center));
        if (textLayout is null) return;

        var hitTestMetrics = textLayout.HitTestTextPosition(state.CaretPosition, false);
        float caretAbsoluteX = hitTestMetrics.Point.X;

        float viewStart = state.ScrollPixelOffset;
        float viewEnd = state.ScrollPixelOffset + availableWidth;
        const float caretWidth = 1.0f;
        const float caretVisibilityPadding = 1.0f;

        if (caretAbsoluteX + caretWidth > viewEnd)
        {
            state.ScrollPixelOffset = (caretAbsoluteX + caretWidth + caretVisibilityPadding) - availableWidth;
        }
        else if (caretAbsoluteX < viewStart)
        {
            state.ScrollPixelOffset = caretAbsoluteX;
        }

        float maxScroll = Math.Max(0, textLayout.Size.X + caretWidth + caretVisibilityPadding - availableWidth);
        state.ScrollPixelOffset = Math.Clamp(state.ScrollPixelOffset, 0, maxScroll);
    }

    private static void PushUndoState(string text, InputTextState state)
    {
        var lastState = state.UndoStack.Count > 0 ? state.UndoStack.Peek() : default;
        if (state.UndoStack.Count > 0 && lastState.Text == text && lastState.CaretPosition == state.CaretPosition && lastState.SelectionAnchor == state.SelectionAnchor) return;

        if (state.UndoStack.Count >= InputTextState.HistoryLimit) state.UndoStack.Pop();
        state.UndoStack.Push(new LineEditUndoRecord(text, state.CaretPosition, state.SelectionAnchor, state.ScrollPixelOffset));
        state.RedoStack.Clear();
    }

    private static void Undo(ref string text, InputTextState state)
    {
        if (state.UndoStack.Count == 0) return;
        state.RedoStack.Push(new LineEditUndoRecord(text, state.CaretPosition, state.SelectionAnchor, state.ScrollPixelOffset));
        var lastState = state.UndoStack.Pop();
        text = lastState.Text;
        state.CaretPosition = lastState.CaretPosition;
        state.SelectionAnchor = lastState.SelectionAnchor;
        state.ScrollPixelOffset = lastState.ScrollPixelOffset;
    }

    private static void Redo(ref string text, InputTextState state)
    {
        if (state.RedoStack.Count == 0) return;
        state.UndoStack.Push(new LineEditUndoRecord(text, state.CaretPosition, state.SelectionAnchor, state.ScrollPixelOffset));
        var nextState = state.RedoStack.Pop();
        text = nextState.Text;
        state.CaretPosition = nextState.CaretPosition;
        state.SelectionAnchor = nextState.SelectionAnchor;
        state.ScrollPixelOffset = nextState.ScrollPixelOffset;
    }
}