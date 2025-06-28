using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;
using D2D = Vortice.Direct2D1;

namespace DirectUI;

public static partial class UI
{
    #region LineEdit
    public static bool LineEdit(string id, ref string text, LineEditDefinition definition)
    {
        if (!IsContextValid() || definition == null) return false;

        var intId = id.GetHashCode();
        var state = State.GetOrCreateElement<LineEditState>(id);
        var theme = definition.Theme ?? State.GetOrCreateElement<ButtonStylePack>(id + "_theme");
        var input = Context.InputState;

        var position = Context.ApplyLayout(definition.Position);
        Rect bounds = new(position.X, position.Y, definition.Size.X, definition.Size.Y);

        bool isFocused = State.FocusedElementId == intId;
        bool textChanged = false;

        // --- Focus Management ---
        bool isHovering = bounds.Contains(input.MousePosition.X, input.MousePosition.Y);
        if (input.WasLeftMousePressedThisFrame && isHovering && !definition.Disabled)
        {
            State.SetFocus(intId);
            State.SetPotentialCaptorForFrame(intId); // Claim the press
            isFocused = true;
        }

        // --- Input Processing ---
        if (isFocused && !definition.Disabled)
        {
            textChanged = ProcessLineEditInput(id, ref text, state, definition, input);
        }

        // --- Drawing ---
        theme.UpdateCurrentStyle(isHovering, false, definition.Disabled, isFocused);
        Resources.DrawBoxStyleHelper(Context.RenderTarget, position, definition.Size, theme.Current);

        Context.RenderTarget.PushAxisAlignedClip(bounds, D2D.AntialiasMode.PerPrimitive);

        string textToDraw = text;
        if (string.IsNullOrEmpty(text) && !isFocused)
        {
            DrawLineEditText(definition.PlaceholderText, state, definition, theme.Disabled, position);
        }
        else
        {
            if (definition.IsPassword) textToDraw = new string(definition.PasswordChar, text.Length);
            DrawLineEditText(textToDraw, state, definition, theme.Current, position);
        }

        if (isFocused && state.IsBlinkOn)
        {
            DrawLineEditCaret(textToDraw, state, definition, theme.Current, position);
        }

        Context.RenderTarget.PopAxisAlignedClip();

        Context.AdvanceLayout(definition.Size);
        return textChanged;
    }

    private static bool ProcessLineEditInput(string id, ref string text, LineEditState state, LineEditDefinition def, InputState input)
    {
        bool textChanged = false;

        state.BlinkTimer += 0.016f; // Rough approximation of delta time
        if (state.BlinkTimer > 0.5f)
        {
            state.BlinkTimer = 0;
            state.IsBlinkOn = !state.IsBlinkOn;
        }

        bool isCtrlHeld = input.HeldKeys.Contains(Keys.Control);

        if (input.TypedCharacters.Any())
        {
            PushUndoState(state, text);
            foreach (char c in input.TypedCharacters)
            {
                if (text.Length < def.MaxLength)
                {
                    text = text.Insert(state.CaretPosition, c.ToString());
                    state.CaretPosition++;
                    textChanged = true;
                }
            }
        }

        foreach (var key in input.PressedKeys)
        {
            bool hasChanged = true;
            PushUndoState(state, text); // Push on first key press
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
                case Keys.Z when isCtrlHeld: Undo(state, ref text); break;
                case Keys.Y when isCtrlHeld: Redo(state, ref text); break;
                default: hasChanged = false; break;
            }
            if (hasChanged) textChanged = true;
        }

        state.CaretPosition = Math.Clamp(state.CaretPosition, 0, text.Length);

        if (textChanged)
        {
            UpdateLineEditView(text, state, def.Size, def.TextMargin);
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
        while (pos < text.Length && !char.IsWhiteSpace(text[pos])) pos++;
        while (pos < text.Length && char.IsWhiteSpace(text[pos])) pos++;
        return pos;
    }

    private static void DrawLineEditText(string textToDraw, LineEditState state, LineEditDefinition def, ButtonStyle style, Vector2 position)
    {
        if (string.IsNullOrEmpty(textToDraw)) return;

        var textBrush = Resources.GetOrCreateBrush(Context.RenderTarget, style.FontColor);
        var textFormat = Resources.GetOrCreateTextFormat(Context.DWriteFactory, style);
        if (textBrush is null || textFormat is null) return;

        textFormat.WordWrapping = WordWrapping.NoWrap;
        textFormat.TextAlignment = Vortice.DirectWrite.TextAlignment.Leading;
        textFormat.ParagraphAlignment = ParagraphAlignment.Center;

        string visibleText = textToDraw;
        if (state.TextStartIndex > 0 && state.TextStartIndex < textToDraw.Length)
        {
            visibleText = textToDraw.Substring(state.TextStartIndex);
        }
        else if (state.TextStartIndex >= textToDraw.Length)
        {
            visibleText = "";
        }

        Rect layoutRect = new Rect(
            position.X + def.TextMargin.X,
            position.Y + def.TextMargin.Y,
            Math.Max(0, def.Size.X - def.TextMargin.X * 2),
            Math.Max(0, def.Size.Y - def.TextMargin.Y * 2)
        );

        Context.RenderTarget.DrawText(visibleText, textFormat, layoutRect, textBrush, DrawTextOptions.Clip);
    }

    private static void DrawLineEditCaret(string text, LineEditState state, LineEditDefinition def, ButtonStyle style, Vector2 position)
    {
        if (state.CaretPosition < state.TextStartIndex) return;

        string textBeforeCaret = text.Substring(state.TextStartIndex, state.CaretPosition - state.TextStartIndex);
        float caretXOffset = MeasureTextWidth(textBeforeCaret, style);

        Rect caretRect = new Rect(
            position.X + def.TextMargin.X + caretXOffset,
            position.Y + def.TextMargin.Y,
            1, // Caret width
            Math.Max(0, def.Size.Y - def.TextMargin.Y * 2)
        );

        var caretBrush = Resources.GetOrCreateBrush(Context.RenderTarget, style.FontColor);
        if (caretBrush != null)
        {
            Context.RenderTarget.FillRectangle(caretRect, caretBrush);
        }
    }

    private static float MeasureTextWidth(string text, ButtonStyle style)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        var textFormat = Resources.GetOrCreateTextFormat(Context.DWriteFactory, style);
        if (textFormat is null) return 0;
        textFormat.WordWrapping = WordWrapping.NoWrap;
        using var textLayout = Context.DWriteFactory.CreateTextLayout(text, textFormat, float.MaxValue, float.MaxValue);
        return textLayout.Metrics.WidthIncludingTrailingWhitespace;
    }

    private static void UpdateLineEditView(string text, LineEditState state, Vector2 size, Vector2 margin)
    {
        float availableWidth = size.X - margin.X * 2;
        var style = new ButtonStyle(); // A default style for measurement

        string textBeforeCaret = text.Substring(0, state.CaretPosition);
        float caretPixelX = MeasureTextWidth(textBeforeCaret, style);

        string textInView = text.Substring(state.TextStartIndex);
        float viewStartX = MeasureTextWidth(text.Substring(0, state.TextStartIndex), style);

        float caretPosInView = caretPixelX - viewStartX;

        if (caretPosInView > availableWidth)
        {
            state.TextStartIndex = state.CaretPosition - (int)(availableWidth / MeasureTextWidth(" ", style));
        }
        else if (caretPosInView < 0)
        {
            state.TextStartIndex = state.CaretPosition;
        }

        state.TextStartIndex = Math.Clamp(state.TextStartIndex, 0, text.Length);
    }

    private static void PushUndoState(LineEditState state, string text)
    {
        if (state.UndoStack.Count > 0 && state.UndoStack.Peek().Text == text) return;
        if (state.UndoStack.Count >= LineEditState.HistoryLimit) state.UndoStack.Pop();
        state.UndoStack.Push(new LineEditUndoRecord(text, state.CaretPosition, state.TextStartIndex));
        state.RedoStack.Clear();
    }

    private static void Undo(LineEditState state, ref string text)
    {
        if (state.UndoStack.Count == 0) return;
        state.RedoStack.Push(new LineEditUndoRecord(text, state.CaretPosition, state.TextStartIndex));
        var lastState = state.UndoStack.Pop();
        text = lastState.Text;
        state.CaretPosition = lastState.CaretPosition;
        state.TextStartIndex = lastState.TextStartIndex;
    }

    private static void Redo(LineEditState state, ref string text)
    {
        if (state.RedoStack.Count == 0) return;
        state.UndoStack.Push(new LineEditUndoRecord(text, state.CaretPosition, state.TextStartIndex));
        var nextState = state.RedoStack.Pop();
        text = nextState.Text;
        state.CaretPosition = nextState.CaretPosition;
        state.TextStartIndex = nextState.TextStartIndex;
    }
    #endregion
}