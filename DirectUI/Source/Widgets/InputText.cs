// Widgets/LineEdit.cs
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using DirectUI.Core;
using Vortice.Mathematics;
using D2D = Vortice.Direct2D1;
using Vortice.DirectWrite;

namespace DirectUI;

internal class InputText
{
    public InputTextResult UpdateAndDraw(
        int intId,
        ref string text,
        InputTextState state,
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

        if (theme is null)
        {
            finalTheme.Roundness = 0.2f;
            finalTheme.BorderLength = 1f;
            finalTheme.Normal.FillColor = new Color4(30 / 255f, 30 / 255f, 30 / 255f, 1.0f);
            finalTheme.Normal.BorderColor = DefaultTheme.NormalBorder;
            finalTheme.Hover.FillColor = finalTheme.Normal.FillColor;
            finalTheme.Hover.BorderColor = DefaultTheme.HoverBorder;
            finalTheme.Focused.FillColor = finalTheme.Normal.FillColor;
            finalTheme.Focused.BorderColor = DefaultTheme.FocusBorder;
            finalTheme.Disabled.FillColor = finalTheme.Normal.FillColor;
            finalTheme.Disabled.BorderColor = DefaultTheme.DisabledBorder;
        }

        Rect bounds = new(position.X, position.Y, size.X, size.Y);
        bool textChanged = false;
        bool enterPressed = false;

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
        if (isPressed && input.IsLeftMouseDown && input.MousePosition != input.PreviousMousePosition)
        {
            int newCaretPos = GetCaretPositionFromMouse(input.MousePosition, text, state, isPassword, passwordChar, currentStyle, bounds, textMargin, textService);
            if (newCaretPos != state.CaretPosition)
            {
                state.CaretPosition = newCaretPos;
                UpdateView(text, state, size, textMargin);
            }
        }
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
            (textChanged, enterPressed) = ProcessInput(ref text, state, size, maxLength, textMargin, input);
        }

        // --- Drawing ---
        renderer.DrawBox(bounds, finalTheme.Current);

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
                FontColor = new Color4(100 / 255f, 100 / 255f, 100 / 255f, 1.0f)
            };
        }
        else
        {
            textToDraw = isPassword ? new string(passwordChar, text.Length) : text;
            styleToDraw = finalTheme.Current;

            // DEBUG: Log what we're about to draw
            if (input.TypedCharacters.Any() && !isPassword)
            {
                DebugStringInfo("Text to draw: " + textToDraw);
            }
        }

        DrawSelectionHighlight(textToDraw, state, styleToDraw, contentRect);
        DrawVisibleText(textToDraw, state, size, styleToDraw, contentRect.TopLeft);

        if (isFocused && state.IsBlinkOn)
        {
            DrawCaret(textToDraw, state, size, finalTheme.Current, contentRect);
        }

        renderer.PopClipRect();

        return new InputTextResult(textChanged, enterPressed);
    }

    private void DebugStringInfo(string text)
    {
        StringInfo si = new StringInfo(text);
        StringBuilder debug = new StringBuilder();
        debug.AppendLine($"=== STRING DEBUG ===");
        debug.AppendLine($"Text: '{text}'");
        debug.AppendLine($"Length (chars): {text.Length}");
        debug.AppendLine($"Graphemes: {si.LengthInTextElements}");
        debug.AppendLine($"Codepoints: {text.EnumerateRunes().Count()}");

        for (int i = 0; i < si.LengthInTextElements; i++)
        {
            string grapheme = si.SubstringByTextElements(i, 1);
            debug.AppendLine($"  Grapheme {i}: '{grapheme}' (Char length: {grapheme.Length})");

            // Show individual codepoints
            var runes = grapheme.EnumerateRunes();
            int runeIndex = 0;
            foreach (Rune rune in runes)
            {
                debug.AppendLine($"    Rune {runeIndex}: U+{rune.Value:X4} '{rune}'");
                runeIndex++;
            }
        }

        // Also show raw bytes for debugging
        byte[] utf8Bytes = Encoding.UTF8.GetBytes(text);
        debug.AppendLine($"UTF-8 Bytes: {BitConverter.ToString(utf8Bytes)}");

        System.Diagnostics.Debug.WriteLine(debug.ToString());
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

    private (bool textChanged, bool enterPressed) ProcessInput(ref string text, InputTextState state, Vector2 size, int maxLength, Vector2 textMargin, InputState input)
    {
        bool textChanged = false;
        bool enterPressed = false;

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

            var chars = input.TypedCharacters.ToArray();

            // Apply PUA correction to each character
            string typedString = string.Concat(chars.Select(c => CorrectPuaCharacter(c)));

            System.Diagnostics.Debug.WriteLine($"After PUA correction: '{typedString}' (Length: {typedString.Length})");

            // Now process this as graphemes
            StringInfo si = new StringInfo(typedString);
            for (int i = 0; i < si.LengthInTextElements; i++)
            {
                string grapheme = si.SubstringByTextElements(i, 1);

                if (text.Length + grapheme.Length > maxLength)
                    break;

                text = text.Insert(state.CaretPosition, grapheme);
                state.CaretPosition += grapheme.Length;
                textChanged = true;

                System.Diagnostics.Debug.WriteLine($"Inserted corrected grapheme: '{grapheme}' (Length: {grapheme.Length})");
            }

            if (textChanged)
            {
                state.SelectionAnchor = state.CaretPosition;
            }
        }

        foreach (var key in input.PressedKeys)
        {
            bool hasTextChangedThisKey = false;
            PushUndoState(text, state);
            switch (key)
            {
                case Keys.Enter:
                    enterPressed = true;
                    break;
                case Keys.A when isCtrlHeld:
                    state.SelectionAnchor = 0;
                    state.CaretPosition = text.Length;
                    break;
                case Keys.Backspace:
                    if (state.HasSelection)
                    {
                        text = text.Remove(state.SelectionStart, state.SelectionEnd - state.SelectionStart);
                        state.CaretPosition = state.SelectionStart;
                        hasTextChangedThisKey = true;
                    }
                    else if (state.CaretPosition > 0)
                    {
                        if (isCtrlHeld)
                        {
                            int removeCount = state.CaretPosition - FindPreviousWordStart(text, state.CaretPosition);
                            text = text.Remove(state.CaretPosition - removeCount, removeCount);
                            state.CaretPosition -= removeCount;
                        }
                        else
                        {
                            int prevBoundary = FindPreviousGraphemeBoundary(text, state.CaretPosition);
                            int graphemeLength = state.CaretPosition - prevBoundary;
                            text = text.Remove(prevBoundary, graphemeLength);
                            state.CaretPosition = prevBoundary;
                        }
                        hasTextChangedThisKey = true;
                    }
                    if (hasTextChangedThisKey) state.SelectionAnchor = state.CaretPosition;
                    break;
                case Keys.Delete:
                    if (state.HasSelection)
                    {
                        text = text.Remove(state.SelectionStart, state.SelectionEnd - state.SelectionStart);
                        state.CaretPosition = state.SelectionStart;
                        hasTextChangedThisKey = true;
                    }
                    else if (state.CaretPosition < text.Length)
                    {
                        if (isCtrlHeld)
                        {
                            int removeCount = FindNextWordEnd(text, state.CaretPosition) - state.CaretPosition;
                            text = text.Remove(state.CaretPosition, removeCount);
                        }
                        else
                        {
                            int nextBoundary = FindNextGraphemeBoundary(text, state.CaretPosition);
                            int graphemeLength = nextBoundary - state.CaretPosition;
                            text = text.Remove(state.CaretPosition, graphemeLength);
                        }
                        hasTextChangedThisKey = true;
                    }
                    if (hasTextChangedThisKey) state.SelectionAnchor = state.CaretPosition;
                    break;
                case Keys.LeftArrow:
                    if (isShiftHeld)
                    {
                        var newPos = isCtrlHeld ? FindPreviousWordStart(text, state.CaretPosition) : FindPreviousGraphemeBoundary(text, state.CaretPosition);
                        state.CaretPosition = newPos;
                    }
                    else
                    {
                        var newPos = state.HasSelection ? state.SelectionStart : (isCtrlHeld ? FindPreviousWordStart(text, state.CaretPosition) : FindPreviousGraphemeBoundary(text, state.CaretPosition));
                        state.CaretPosition = newPos;
                        state.SelectionAnchor = state.CaretPosition;
                    }
                    break;
                case Keys.RightArrow:
                    if (isShiftHeld)
                    {
                        var newPos = isCtrlHeld ? FindNextWordEnd(text, state.CaretPosition) : FindNextGraphemeBoundary(text, state.CaretPosition);
                        state.CaretPosition = newPos;
                    }
                    else
                    {
                        var newPos = state.HasSelection ? state.SelectionEnd : (isCtrlHeld ? FindNextWordEnd(text, state.CaretPosition) : FindNextGraphemeBoundary(text, state.CaretPosition));
                        state.CaretPosition = newPos;
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

        return (textChanged, enterPressed);
    }

    private void DebugTypedCharacters(IEnumerable<char> typedChars)
    {
        var chars = typedChars.ToArray();
        StringBuilder debug = new StringBuilder();
        debug.AppendLine($"=== TYPED CHARACTERS DEBUG ===");
        debug.AppendLine($"Count: {chars.Length}");

        for (int i = 0; i < chars.Length; i++)
        {
            char c = chars[i];
            debug.AppendLine($"  Char {i}: '{c}' (U+{(ushort)c:X4}) - High surrogate: {char.IsHighSurrogate(c)}, Low surrogate: {char.IsLowSurrogate(c)}");
        }

        string asString = new string(chars);
        debug.AppendLine($"As string: '{asString}'");

        StringInfo si = new StringInfo(asString);
        debug.AppendLine($"Graphemes in typed: {si.LengthInTextElements}");

        System.Diagnostics.Debug.WriteLine(debug.ToString());
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

    private static int FindNextGraphemeBoundary(string text, int currentPosition)
    {
        if (currentPosition >= text.Length) return text.Length;

        StringInfo si = new StringInfo(text);
        int textElementIndex = 0;
        int charIndex = 0;

        while (charIndex <= currentPosition && textElementIndex < si.LengthInTextElements)
        {
            string element = si.SubstringByTextElements(textElementIndex, 1);
            if (charIndex + element.Length > currentPosition)
            {
                return charIndex + element.Length;
            }
            charIndex += element.Length;
            textElementIndex++;
        }

        return text.Length;
    }

    private static int FindPreviousGraphemeBoundary(string text, int currentPosition)
    {
        if (currentPosition <= 0) return 0;

        StringInfo si = new StringInfo(text);
        int textElementIndex = 0;
        int charIndex = 0;

        while (charIndex < currentPosition && textElementIndex < si.LengthInTextElements)
        {
            string element = si.SubstringByTextElements(textElementIndex, 1);
            int nextCharIndex = charIndex + element.Length;

            if (nextCharIndex >= currentPosition)
            {
                return charIndex;
            }

            charIndex = nextCharIndex;
            textElementIndex++;
        }

        return currentPosition;
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
        selectionColor.A = 100;
        var selectionStyle = new BoxStyle { FillColor = selectionColor, Roundness = 0f, BorderLength = 0f };

        renderer.DrawBox(highlightRect, selectionStyle);
    }

    private void DrawVisibleText(string fullText, InputTextState state, Vector2 size, ButtonStyle style, Vector2 contentTopLeft)
    {
        if (string.IsNullOrEmpty(fullText)) return;

        var context = UI.Context;
        var renderer = context.Renderer;
        var textService = context.TextService;

        // Create a modified style with emoji font support
        var emojiStyle = new ButtonStyle(style);
        emojiStyle.FontName = "Segoe UI Emoji, Noto Color Emoji, Apple Color Emoji, sans-serif";
        emojiStyle.FontSize = style.FontSize * 1.2f; // Slightly larger for better emoji rendering

        var textLayout = textService.GetTextLayout(fullText, emojiStyle, new(float.MaxValue, size.Y),
            new Alignment(HAlignment.Left, VAlignment.Center));
        if (textLayout is null) return;

        const float yOffsetCorrection = -1.5f;
        Vector2 drawOrigin = new Vector2(contentTopLeft.X - state.ScrollPixelOffset, contentTopLeft.Y + yOffsetCorrection);

        // Use the emoji style for rendering
        renderer.DrawText(drawOrigin, fullText, emojiStyle,
            new Alignment(HAlignment.Left, VAlignment.Center),
            new Vector2(float.MaxValue, size.Y), emojiStyle.FontColor);
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
        var style = new ButtonStyle();

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

    private void DebugSurrogateHandling(IEnumerable<char> typedChars)
    {
        var chars = typedChars.ToArray();
        StringBuilder debug = new StringBuilder();
        debug.AppendLine($"=== SURROGATE PAIR DEBUG ===");

        for (int i = 0; i < chars.Length; i++)
        {
            char c = chars[i];
            bool isHigh = char.IsHighSurrogate(c);
            bool isLow = char.IsLowSurrogate(c);

            debug.AppendLine($"Char {i}: '{c}' (U+{(ushort)c:X4}) - High: {isHigh}, Low: {isLow}");

            // Check for complete surrogate pair
            if (isHigh && i + 1 < chars.Length && char.IsLowSurrogate(chars[i + 1]))
            {
                int codePoint = char.ConvertToUtf32(c, chars[i + 1]);
                debug.AppendLine($"  >>> SURROGATE PAIR: U+{codePoint:X4} '{char.ConvertFromUtf32(codePoint)}'");
                i++; // Skip low surrogate
            }
            else if (isHigh || isLow)
            {
                debug.AppendLine($"  >>> INCOMPLETE SURROGATE PAIR!");
            }
        }

        System.Diagnostics.Debug.WriteLine(debug.ToString());
    }



    private string CorrectPuaCharacter(char c)
    {
        // Map common PUA characters back to their real emoji equivalents
        // This is a workaround for the input system corruption
        return c switch
        {
            '\uF600' => "\U0001F600", // 😀
            '\uF601' => "\U0001F601", // 😁
            '\uF602' => "\U0001F602", // 😂
            '\uF603' => "\U0001F603", // 😃
            '\uF604' => "\U0001F604", // 😄
            '\uF605' => "\U0001F605", // 😅
            '\uF606' => "\U0001F606", // 😆
            '\uF607' => "\U0001F607", // 😇
            '\uF608' => "\U0001F608", // 😈
            '\uF609' => "\U0001F609", // 😉
            '\uF60A' => "\U0001F60A", // 😊
            '\uF60B' => "\U0001F60B", // 😋
            '\uF60C' => "\U0001F60C", // 😌
            '\uF60D' => "\U0001F60D", // 😍
            '\uF60E' => "\U0001F60E", // 😎
            '\uF60F' => "\U0001F60F", // 😏
            '\uF389' => "\U0001F389", // 🎉
            '\uF9E0' => "\U0001F9E0", // 🧠
            '\uF9E1' => "\U0001F9E1", // 🧡
            '\uF9E2' => "\U0001F9E2", // 🧢
            '\uF9E3' => "\U0001F9E3", // 🧣
            '\uF9E4' => "\U0001F9E4", // 🧤
            '\uF9E5' => "\U0001F9E5", // 🧥
            '\uF9E6' => "\U0001F9E6", // 🧦
            '\uF9E7' => "\U0001F9E7", // 🧧
            '\uF9E8' => "\U0001F9E8", // 🧨
            '\uF9E9' => "\U0001F9E9", // 🧩
            '\uF9EA' => "\U0001F9EA", // 🧪
            '\uF9EB' => "\U0001F9EB", // 🧫
            '\uF9EC' => "\U0001F9EC", // 🧬
            '\uF9ED' => "\U0001F9ED", // 🧭
            '\uF9EE' => "\U0001F9EE", // 🧮
            '\uF9EF' => "\U0001F9EF", // 🧯
            '\uF9F0' => "\U0001F9F0", // 🧰
            '\uF9F1' => "\U0001F9F1", // 🧱
            '\uF9F2' => "\U0001F9F2", // 🧲
            '\uF9F3' => "\U0001F9F3", // 🧳
            '\uF9F4' => "\U0001F9F4", // 🧴
            '\uF9F5' => "\U0001F9F5", // 🧵
            '\uF9F6' => "\U0001F9F6", // 🧶
            '\uF9F7' => "\U0001F9F7", // 🧷
            '\uF9F8' => "\U0001F9F8", // 🧸
            '\uF9F9' => "\U0001F9F9", // 🧹
            '\uF9FA' => "\U0001F9FA", // 🧺
            '\uF9FB' => "\U0001F9FB", // 🧻
            '\uF9FC' => "\U0001F9FC", // 🧼
            '\uF9FD' => "\U0001F9FD", // 🧽
            '\uF9FE' => "\U0001F9FE", // 🧾
            '\uF9FF' => "\U0001F9FF", // 🧿
            _ => c.ToString()
        };
    }
}