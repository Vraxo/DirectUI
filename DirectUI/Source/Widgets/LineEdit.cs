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
internal record struct LineEditUndoRecord(string Text, int CaretPosition, int TextStartIndex);

// State and logic for an immediate-mode LineEdit control, encapsulated in a single class.
internal class LineEdit
{
    // === PERSISTENT STATE ===
    private int _caretPosition;
    private int _textStartIndex; // The starting character index for the visible text portion

    private float _blinkTimer;
    private bool _isBlinkOn = true;

    private readonly Stack<LineEditUndoRecord> _undoStack = new();
    private readonly Stack<LineEditUndoRecord> _redoStack = new();
    private const int HistoryLimit = 50;

    // === MAIN UPDATE & DRAW METHOD ===
    public bool UpdateAndDraw(string id, ref string text, LineEditDefinition definition)
    {
        var context = UI.Context;
        var state = UI.State;
        var resources = UI.Resources;

        var intId = id.GetHashCode();
        var theme = definition.Theme ?? state.GetOrCreateElement<ButtonStylePack>(id + "_theme");
        var input = context.InputState;

        var position = context.Layout.ApplyLayout(definition.Position);
        Rect bounds = new(position.X, position.Y, definition.Size.X, definition.Size.Y);

        bool isFocused = state.FocusedElementId == intId;
        bool textChanged = false;

        // --- Focus Management ---
        bool isHovering = bounds.Contains(input.MousePosition.X, input.MousePosition.Y);
        if (input.WasLeftMousePressedThisFrame && isHovering && !definition.Disabled)
        {
            state.SetFocus(intId);
            state.SetPotentialCaptorForFrame(intId); // Claim the press
            isFocused = true;
        }

        // --- Input Processing ---
        if (isFocused && !definition.Disabled)
        {
            textChanged = ProcessInput(ref text, definition, input);
        }

        // --- Drawing ---
        theme.UpdateCurrentStyle(isHovering, false, definition.Disabled, isFocused);
        resources.DrawBoxStyleHelper(context.RenderTarget, position, definition.Size, theme.Current);

        context.RenderTarget.PushAxisAlignedClip(bounds, D2D.AntialiasMode.PerPrimitive);

        string textToDraw = text;
        if (string.IsNullOrEmpty(text) && !isFocused)
        {
            DrawText(definition.PlaceholderText, definition, theme.Disabled, position);
        }
        else
        {
            if (definition.IsPassword) textToDraw = new string(definition.PasswordChar, text.Length);
            DrawText(textToDraw, definition, theme.Current, position);
        }

        if (isFocused && _isBlinkOn)
        {
            DrawCaret(textToDraw, definition, theme.Current, position);
        }

        context.RenderTarget.PopAxisAlignedClip();

        return textChanged;
    }

    private bool ProcessInput(ref string text, LineEditDefinition def, InputState input)
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
                if (text.Length < def.MaxLength)
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

        if (textChanged)
        {
            UpdateView(text, def.Size, def.TextMargin);
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

    private void DrawText(string textToDraw, LineEditDefinition def, ButtonStyle style, Vector2 position)
    {
        if (string.IsNullOrEmpty(textToDraw)) return;

        var context = UI.Context;
        var resources = UI.Resources;

        var textBrush = resources.GetOrCreateBrush(context.RenderTarget, style.FontColor);
        var textFormat = resources.GetOrCreateTextFormat(context.DWriteFactory, style);
        if (textBrush is null || textFormat is null) return;

        textFormat.WordWrapping = WordWrapping.NoWrap;
        textFormat.TextAlignment = Vortice.DirectWrite.TextAlignment.Leading;
        textFormat.ParagraphAlignment = ParagraphAlignment.Center;

        string visibleText = textToDraw;
        if (_textStartIndex > 0 && _textStartIndex < textToDraw.Length)
        {
            visibleText = textToDraw.Substring(_textStartIndex);
        }
        else if (_textStartIndex >= textToDraw.Length)
        {
            visibleText = "";
        }

        Rect layoutRect = new Rect(
            position.X + def.TextMargin.X,
            position.Y + def.TextMargin.Y,
            Math.Max(0, def.Size.X - def.TextMargin.X * 2),
            Math.Max(0, def.Size.Y - def.TextMargin.Y * 2)
        );

        context.RenderTarget.DrawText(visibleText, textFormat, layoutRect, textBrush, DrawTextOptions.Clip);
    }

    private void DrawCaret(string text, LineEditDefinition def, ButtonStyle style, Vector2 position)
    {
        if (_caretPosition < _textStartIndex) return;

        string textBeforeCaret = text.Substring(_textStartIndex, _caretPosition - _textStartIndex);
        float caretXOffset = MeasureTextWidth(textBeforeCaret, style);

        Rect caretRect = new Rect(
            position.X + def.TextMargin.X + caretXOffset,
            position.Y + def.TextMargin.Y,
            1, // Caret width
            Math.Max(0, def.Size.Y - def.TextMargin.Y * 2)
        );

        var context = UI.Context;
        var caretBrush = UI.Resources.GetOrCreateBrush(context.RenderTarget, style.FontColor);
        if (caretBrush != null)
        {
            context.RenderTarget.FillRectangle(caretRect, caretBrush);
        }
    }

    private float MeasureTextWidth(string text, ButtonStyle style)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        var context = UI.Context;
        var textFormat = UI.Resources.GetOrCreateTextFormat(context.DWriteFactory, style);
        if (textFormat is null) return 0;
        textFormat.WordWrapping = WordWrapping.NoWrap;
        using var textLayout = context.DWriteFactory.CreateTextLayout(text, textFormat, float.MaxValue, float.MaxValue);
        return textLayout.Metrics.WidthIncludingTrailingWhitespace;
    }

    private void UpdateView(string text, Vector2 size, Vector2 margin)
    {
        float availableWidth = size.X - margin.X * 2;
        var style = new ButtonStyle(); // A default style for measurement

        string textBeforeCaret = text.Substring(0, _caretPosition);
        float caretPixelX = MeasureTextWidth(textBeforeCaret, style);

        string textInView = text.Substring(_textStartIndex);
        float viewStartX = MeasureTextWidth(text.Substring(0, _textStartIndex), style);

        float caretPosInView = caretPixelX - viewStartX;

        if (caretPosInView > availableWidth)
        {
            _textStartIndex = _caretPosition - (int)(availableWidth / MeasureTextWidth(" ", style));
        }
        else if (caretPosInView < 0)
        {
            _textStartIndex = _caretPosition;
        }

        _textStartIndex = Math.Clamp(_textStartIndex, 0, text.Length);
    }

    private void PushUndoState(string text)
    {
        if (_undoStack.Count > 0 && _undoStack.Peek().Text == text) return;
        if (_undoStack.Count >= HistoryLimit) _undoStack.Pop();
        _undoStack.Push(new LineEditUndoRecord(text, _caretPosition, _textStartIndex));
        _redoStack.Clear();
    }

    private void Undo(ref string text)
    {
        if (_undoStack.Count == 0) return;
        _redoStack.Push(new LineEditUndoRecord(text, _caretPosition, _textStartIndex));
        var lastState = _undoStack.Pop();
        text = lastState.Text;
        _caretPosition = lastState.CaretPosition;
        _textStartIndex = lastState.TextStartIndex;
    }

    private void Redo(ref string text)
    {
        if (_redoStack.Count == 0) return;
        _undoStack.Push(new LineEditUndoRecord(text, _caretPosition, _textStartIndex));
        var nextState = _redoStack.Pop();
        text = nextState.Text;
        _caretPosition = nextState.CaretPosition;
        _textStartIndex = nextState.TextStartIndex;
    }
}