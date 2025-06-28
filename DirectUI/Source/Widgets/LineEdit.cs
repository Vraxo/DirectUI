using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;
using D2D = Vortice.Direct2D1;

namespace DirectUI;

// Record for undo/redo functionality
internal record struct LineEditUndoRecord(string Text, int CaretPosition, float ScrollPixelOffset);

// State and logic for an immediate-mode LineEdit control, encapsulated in a single class.
internal class LineEdit
{
    // === PERSISTENT STATE ===
    private int _caretPosition;
    private float _scrollPixelOffset; // The horizontal scroll position in pixels

    private float _blinkTimer;
    private bool _isBlinkOn = true;

    private readonly Stack<LineEditUndoRecord> _undoStack = new();
    private readonly Stack<LineEditUndoRecord> _redoStack = new();
    private const int HistoryLimit = 50;

    // === MAIN UPDATE & DRAW METHOD ===
    public bool UpdateAndDraw(
        int intId,
        ref string text,
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
        var state = UI.State;
        var resources = UI.Resources;

        var themeId = HashCode.Combine(intId, "theme");
        var finalTheme = theme ?? state.GetOrCreateElement<ButtonStylePack>(themeId);
        var input = context.InputState;

        Rect bounds = new(position.X, position.Y, size.X, size.Y);

        bool isFocused = state.FocusedElementId == intId;
        bool textChanged = false;

        // --- Focus Management ---
        bool isHovering = bounds.Contains(input.MousePosition.X, input.MousePosition.Y);
        if (input.WasLeftMousePressedThisFrame && isHovering && !disabled)
        {
            state.SetFocus(intId);
            state.SetPotentialCaptorForFrame(intId); // Claim the press
            isFocused = true;
        }

        // --- Input Processing ---
        if (isFocused && !disabled)
        {
            textChanged = ProcessInput(ref text, size, maxLength, textMargin, input);
        }

        // --- Drawing ---
        finalTheme.UpdateCurrentStyle(isHovering, false, disabled, isFocused);
        resources.DrawBoxStyleHelper(context.RenderTarget, position, size, finalTheme.Current);

        // Define content area and clip to it
        Rect contentRect = new Rect(
            bounds.X + textMargin.X,
            bounds.Y + textMargin.Y,
            Math.Max(0, bounds.Width - textMargin.X * 2),
            Math.Max(0, bounds.Height - textMargin.Y * 2)
        );
        context.RenderTarget.PushAxisAlignedClip(contentRect, D2D.AntialiasMode.PerPrimitive);

        var textToDraw = isPassword ? new string(passwordChar, text.Length) : text;

        if (string.IsNullOrEmpty(text) && !isFocused)
        {
            DrawVisibleText(placeholderText, size, textMargin, finalTheme.Disabled, contentRect.TopLeft);
        }
        else
        {
            DrawVisibleText(textToDraw, size, textMargin, finalTheme.Current, contentRect.TopLeft);
        }

        if (isFocused && _isBlinkOn)
        {
            DrawCaret(textToDraw, size, textMargin, finalTheme.Current, contentRect);
        }

        context.RenderTarget.PopAxisAlignedClip();

        return textChanged;
    }

    private bool ProcessInput(ref string text, Vector2 size, int maxLength, Vector2 textMargin, InputState input)
    {
        bool textChanged = false;

        _blinkTimer += 0.016f; // Rough approximation of delta time
        if (_blinkTimer > 0.5f)
        {
            _blinkTimer = 0;
            _isBlinkOn = !_isBlinkOn;
        }

        bool isCtrlHeld = input.HeldKeys.Contains(Keys.Control);

        if (input.TypedCharacters.Any())
        {
            PushUndoState(text);
            foreach (char c in input.TypedCharacters)
            {
                if (text.Length < maxLength)
                {
                    text = text.Insert(_caretPosition, c.ToString());
                    _caretPosition++;
                    textChanged = true;
                }
            }
        }

        foreach (var key in input.PressedKeys)
        {
            bool hasChanged = true;
            PushUndoState(text); // Push on first key press
            switch (key)
            {
                case Keys.Backspace:
                    if (_caretPosition > 0)
                    {
                        int removeCount = 1;
                        if (isCtrlHeld) removeCount = _caretPosition - FindPreviousWordStart(text, _caretPosition);
                        text = text.Remove(_caretPosition - removeCount, removeCount);
                        _caretPosition -= removeCount;
                    }
                    break;
                case Keys.Delete:
                    if (_caretPosition < text.Length)
                    {
                        int removeCount = 1;
                        if (isCtrlHeld) removeCount = FindNextWordEnd(text, _caretPosition) - _caretPosition;
                        text = text.Remove(_caretPosition, removeCount);
                    }
                    break;
                case Keys.LeftArrow:
                    _caretPosition = isCtrlHeld ? FindPreviousWordStart(text, _caretPosition) : _caretPosition - 1;
                    break;
                case Keys.RightArrow:
                    _caretPosition = isCtrlHeld ? FindNextWordEnd(text, _caretPosition) : _caretPosition + 1;
                    break;
                case Keys.Home: _caretPosition = 0; break;
                case Keys.End: _caretPosition = text.Length; break;
                case Keys.Z when isCtrlHeld: Undo(ref text); break;
                case Keys.Y when isCtrlHeld: Redo(ref text); break;
                default: hasChanged = false; break;
            }
            if (hasChanged) textChanged = true;
        }

        _caretPosition = Math.Clamp(_caretPosition, 0, text.Length);

        if (textChanged || input.PressedKeys.Any(k => k is Keys.LeftArrow or Keys.RightArrow or Keys.Home or Keys.End))
        {
            UpdateView(text, size, textMargin);
            _isBlinkOn = true;
            _blinkTimer = 0;
        }

        return textChanged;
    }

    private int FindPreviousWordStart(string text, int currentPos)
    {
        if (currentPos == 0) return 0;
        int pos = currentPos - 1;
        while (pos > 0 && char.IsWhiteSpace(text[pos])) pos--;
        while (pos > 0 && !char.IsWhiteSpace(text[pos - 1])) pos--;
        return pos;
    }

    private int FindNextWordEnd(string text, int currentPos)
    {
        if (currentPos >= text.Length) return text.Length;
        int pos = currentPos;
        while (pos < text.Length && !char.IsWhiteSpace(text[pos])) pos++;
        while (pos < text.Length && char.IsWhiteSpace(text[pos])) pos++;
        return pos;
    }

    private void DrawVisibleText(string fullText, Vector2 size, Vector2 textMargin, ButtonStyle style, Vector2 contentTopLeft)
    {
        if (string.IsNullOrEmpty(fullText)) return;

        var context = UI.Context;
        var rt = context.RenderTarget;
        var textBrush = UI.Resources.GetOrCreateBrush(rt, style.FontColor);
        var textLayout = GetTextLayout(fullText, size, style, float.MaxValue); // Get layout for full string

        if (textBrush == null || textLayout == null) return;

        // Apply a transform to scroll the text
        var originalTransform = rt.Transform;
        var translation = Matrix3x2.CreateTranslation(contentTopLeft.X - _scrollPixelOffset, contentTopLeft.Y);
        rt.Transform = translation * originalTransform;

        rt.DrawTextLayout(Vector2.Zero, textLayout, textBrush, DrawTextOptions.None);

        // Restore original transform
        rt.Transform = originalTransform;
    }

    private void DrawCaret(string text, Vector2 size, Vector2 textMargin, ButtonStyle style, Rect contentRect)
    {
        var textLayout = GetTextLayout(text, size, style, float.MaxValue);
        if (textLayout == null) return;

        // Use HitTest to find the exact caret position
        textLayout.HitTestTextPosition((uint)_caretPosition, false, out _, out _, out var hitTestMetrics);

        float caretX = contentRect.Left + hitTestMetrics.Left - _scrollPixelOffset;

        Rect caretRect = new Rect(
            caretX,
            contentRect.Top,
            1, // Caret width
            contentRect.Height
        );

        var context = UI.Context;
        var caretBrush = UI.Resources.GetOrCreateBrush(context.RenderTarget, style.FontColor);
        if (caretBrush != null)
        {
            context.RenderTarget.FillRectangle(caretRect, caretBrush);
        }
    }

    private IDWriteTextLayout? GetTextLayout(string text, Vector2 size, ButtonStyle style, float maxWidth)
    {
        var context = UI.Context;
        var resources = UI.Resources;
        var dwrite = context.DWriteFactory;

        // Use a consistent alignment for caching and creation
        var alignment = new Alignment(HAlignment.Left, VAlignment.Center);
        var layoutKey = new UIResources.TextLayoutCacheKey(text, style, new(maxWidth, size.Y), alignment);

        if (!resources.textLayoutCache.TryGetValue(layoutKey, out var textLayout))
        {
            var textFormat = resources.GetOrCreateTextFormat(dwrite, style);
            if (textFormat == null) return null;

            textFormat.WordWrapping = WordWrapping.NoWrap;
            textFormat.TextAlignment = Vortice.DirectWrite.TextAlignment.Leading;
            textFormat.ParagraphAlignment = ParagraphAlignment.Center;

            textLayout = dwrite.CreateTextLayout(text, textFormat, maxWidth, size.Y);
            resources.textLayoutCache[layoutKey] = textLayout;
        }
        return textLayout;
    }


    private void UpdateView(string text, Vector2 size, Vector2 textMargin)
    {
        float availableWidth = size.X - textMargin.X * 2;
        var style = new ButtonStyle(); // A default style is fine for measuring

        var textLayout = GetTextLayout(text, size, style, float.MaxValue);
        if (textLayout == null) return;

        // Get the absolute pixel position of the caret
        textLayout.HitTestTextPosition((uint)_caretPosition, false, out _, out _, out var hitTestMetrics);
        float caretAbsoluteX = hitTestMetrics.Left;

        // Check if caret is outside the visible area and adjust scroll offset
        if (caretAbsoluteX - _scrollPixelOffset > availableWidth)
        {
            // Caret is past the right edge, scroll right to bring it into view
            _scrollPixelOffset = caretAbsoluteX - availableWidth;
        }
        else if (caretAbsoluteX - _scrollPixelOffset < 0)
        {
            // Caret is before the left edge, scroll left to bring it into view
            _scrollPixelOffset = caretAbsoluteX;
        }

        // Ensure we don't scroll too far if the text is shorter than the view
        float maxScroll = Math.Max(0, textLayout.Metrics.WidthIncludingTrailingWhitespace - availableWidth);
        _scrollPixelOffset = Math.Clamp(_scrollPixelOffset, 0, maxScroll);
    }

    private void PushUndoState(string text)
    {
        var lastState = _undoStack.Count > 0 ? _undoStack.Peek() : default;
        if (_undoStack.Count > 0 && lastState.Text == text && lastState.CaretPosition == _caretPosition) return;

        if (_undoStack.Count >= HistoryLimit) _undoStack.Pop();
        _undoStack.Push(new LineEditUndoRecord(text, _caretPosition, _scrollPixelOffset));
        _redoStack.Clear();
    }

    private void Undo(ref string text)
    {
        if (_undoStack.Count == 0) return;
        _redoStack.Push(new LineEditUndoRecord(text, _caretPosition, _scrollPixelOffset));
        var lastState = _undoStack.Pop();
        text = lastState.Text;
        _caretPosition = lastState.CaretPosition;
        _scrollPixelOffset = lastState.ScrollPixelOffset;
    }

    private void Redo(ref string text)
    {
        if (_redoStack.Count == 0) return;
        _undoStack.Push(new LineEditUndoRecord(text, _caretPosition, _scrollPixelOffset));
        var nextState = _redoStack.Pop();
        text = nextState.Text;
        _caretPosition = nextState.CaretPosition;
        _scrollPixelOffset = nextState.ScrollPixelOffset;
    }
}