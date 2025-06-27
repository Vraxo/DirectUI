using System;
using System.Collections.Generic;
using System.Numerics;
using System.Linq;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;
using D2D = Vortice.Direct2D1;

namespace DirectUI;

public static partial class UI
{
    public static bool Button(string id, ButtonDefinition definition)
    {
        if (!IsContextValid() || definition is null) return false;
        Button buttonInstance = State.GetOrCreateElement<Button>(id);
        buttonInstance.Position = Context.ApplyLayout(definition.Position);
        ApplyButtonDefinition(buttonInstance, definition);

        bool pushedClip = false;
        if (Context.IsInLayoutContainer() && Context.containerStack.Peek() is GridContainerState grid)
        {
            float clipStartY = grid.CurrentDrawPosition.Y; float gridBottomY = grid.StartPosition.Y + grid.AvailableSize.Y;
            float clipHeight = Math.Max(0f, gridBottomY - clipStartY);
            Rect cellClipRect = new Rect(grid.CurrentDrawPosition.X, clipStartY, Math.Max(0f, grid.CellWidth), clipHeight);
            if (Context.RenderTarget is not null && cellClipRect.Width > 0 && cellClipRect.Height > 0)
            { Context.RenderTarget.PushAxisAlignedClip(cellClipRect, D2D.AntialiasMode.Aliased); pushedClip = true; }
        }

        bool clicked = buttonInstance.Update(id);
        if (pushedClip && Context.RenderTarget is not null)
        { Context.RenderTarget.PopAxisAlignedClip(); }

        Context.AdvanceLayout(buttonInstance.Size);
        return clicked;
    }

    private static bool TabButtonPrimitive(string id, string text, Vector2 size, bool isActive, TabStylePack theme, bool disabled)
    {
        if (!IsContextValid()) return false;

        var intId = id.GetHashCode();
        var position = Context.GetCurrentLayoutPosition();
        Rect bounds = new(position.X, position.Y, size.X, size.Y);

        InputState input = Context.InputState;
        bool isHovering = !disabled && bounds.Contains(input.MousePosition.X, input.MousePosition.Y);
        bool wasClicked = false;

        if (isHovering) State.SetPotentialInputTarget(intId);

        if (input.WasLeftMousePressedThisFrame && isHovering && State.PotentialInputTargetId == intId)
        {
            wasClicked = true;
        }

        theme.UpdateCurrentStyle(isHovering, isActive, disabled, false);

        var rt = Context.RenderTarget;
        Resources.DrawBoxStyleHelper(rt, new Vector2(bounds.X, bounds.Y), new Vector2(bounds.Width, bounds.Height), theme.Current);

        var textBrush = Resources.GetOrCreateBrush(rt, theme.Current.FontColor);
        var textFormat = Resources.GetOrCreateTextFormat(Context.DWriteFactory, theme.Current);
        if (textBrush is not null && textFormat is not null && !string.IsNullOrEmpty(text))
        {
            textFormat.TextAlignment = Vortice.DirectWrite.TextAlignment.Center;
            textFormat.ParagraphAlignment = ParagraphAlignment.Center;
            rt.DrawText(text, textFormat, bounds, textBrush);
        }

        Context.AdvanceLayout(size);
        return wasClicked;
    }

    public static void TabBar(string id, string[] tabLabels, ref int activeIndex, TabStylePack? theme = null)
    {
        if (!IsContextValid() || tabLabels is null || tabLabels.Length == 0) return;

        var tabTheme = theme ?? State.GetOrCreateElement<TabStylePack>(id + "_theme_default");
        var textMargin = new Vector2(15, 5);
        float tabHeight = 30f;
        float maxWidth = 0;

        var styleForMeasuring = tabTheme.Normal;
        foreach (var label in tabLabels)
        {
            Vector2 measuredSize = Resources.MeasureText(Context.DWriteFactory, label, styleForMeasuring);
            if (measuredSize.X > maxWidth)
            {
                maxWidth = measuredSize.X;
            }
        }
        float uniformTabWidth = maxWidth + textMargin.X * 2;
        var tabSize = new Vector2(uniformTabWidth, tabHeight);

        BeginHBoxContainer(id + "_hbox", Context.GetCurrentLayoutPosition(), 0);
        for (int i = 0; i < tabLabels.Length; i++)
        {
            bool wasClicked = TabButtonPrimitive(
                id + "_" + i,
                tabLabels[i],
                tabSize,
                i == activeIndex,
                tabTheme,
                false
            );
            if (wasClicked)
            {
                activeIndex = i;
            }
        }
        EndHBoxContainer();
    }

    public static float HSlider(string id, float currentValue, SliderDefinition definition)
    {
        if (!IsContextValid() || definition is null) return currentValue;
        InternalHSliderLogic sliderInstance = State.GetOrCreateElement<InternalHSliderLogic>(id);
        sliderInstance.Position = Context.ApplyLayout(definition.Position);
        ApplySliderDefinition(sliderInstance, definition);
        sliderInstance.Direction = definition.HorizontalDirection;

        float newValue = sliderInstance.UpdateAndDraw(id, currentValue);
        Context.AdvanceLayout(sliderInstance.Size);
        return newValue;
    }

    public static float VSlider(string id, float currentValue, SliderDefinition definition)
    {
        if (!IsContextValid() || definition is null) return currentValue;
        InternalVSliderLogic sliderInstance = State.GetOrCreateElement<InternalVSliderLogic>(id);
        sliderInstance.Position = Context.ApplyLayout(definition.Position);
        ApplySliderDefinition(sliderInstance, definition);
        sliderInstance.Direction = definition.VerticalDirection;

        float newValue = sliderInstance.UpdateAndDraw(id, currentValue);
        Context.AdvanceLayout(sliderInstance.Size);
        return newValue;
    }

    public static void Tree<T>(string id, TreeNode<T> root, out TreeNode<T>? clickedNode, TreeStyle? style = null)
    {
        if (!IsContextValid() || root is null) { clickedNode = null; return; }

        clickedNode = null;
        var treeStyle = style ?? State.GetOrCreateElement<TreeStyle>(id + "_style");

        var treeState = new TreeViewState(id, treeStyle);
        Context.treeStateStack.Push(treeState);
        ProcessTreeNodeRecursive(id.GetHashCode(), 0, root, ref clickedNode);
        Context.treeStateStack.Pop();
    }

    private static void ProcessTreeNodeRecursive<T>(int parentIdHash, int index, TreeNode<T> node, ref TreeNode<T>? clickedNode)
    {
        if (Context.treeStateStack.Count == 0) return;
        var treeState = Context.treeStateStack.Peek();
        var style = treeState.Style;
        var renderTarget = Context.RenderTarget;

        var startLayoutPos = Context.GetCurrentLayoutPosition();
        var brush = Resources.GetOrCreateBrush(renderTarget, style.LineColor);
        if (brush is not null)
        {
            int i = 0;
            foreach (var shouldDrawLine in treeState.IndentLineState)
            {
                if (shouldDrawLine)
                {
                    float x = startLayoutPos.X + (i * style.Indent) + (style.Indent * 0.5f);
                    renderTarget.DrawLine(new Vector2(x, startLayoutPos.Y), new Vector2(x, startLayoutPos.Y + style.RowHeight), brush, 1.0f);
                }
                i++;
            }
            if (treeState.IndentLineState.Count > 0)
            {
                float hLineXStart = startLayoutPos.X + ((treeState.IndentLineState.Count - 1) * style.Indent) + (style.Indent * 0.5f);
                float hLineY = startLayoutPos.Y + style.RowHeight * 0.5f;
                renderTarget.DrawLine(new Vector2(hLineXStart, hLineY), new Vector2(hLineXStart + style.Indent * 0.5f, hLineY), brush, 1.0f);
            }
        }

        float indentSize = treeState.IndentLineState.Count * style.Indent;
        var nodeRowStartPos = startLayoutPos + new Vector2(indentSize, 0);
        float currentX = nodeRowStartPos.X;
        float gap = 5;

        int nodeHash = node.GetHashCode();
        int toggleId = HashCode.Combine(parentIdHash, index, nodeHash, 0);
        int labelId = HashCode.Combine(parentIdHash, index, nodeHash, 1);

        float toggleWidth = style.RowHeight - 4;
        if (node.Children.Count > 0)
        {
            var bounds = new Rect(currentX, nodeRowStartPos.Y, toggleWidth, style.RowHeight);
            if (StatelessButton(toggleId, bounds, node.IsExpanded ? "-" : "+", style.ToggleStyle, new Alignment(HAlignment.Center, VAlignment.Center), DirectUI.Button.ActionMode.Release))
            {
                node.IsExpanded = !node.IsExpanded;
            }
        }
        currentX += toggleWidth;

        currentX += gap;
        var labelStyle = style.NodeLabelStyle;
        var labelTextAlignment = new Alignment(HAlignment.Left, VAlignment.Center);
        float labelMargin = 4;
        var labelSize = Resources.MeasureText(Context.DWriteFactory, node.Text, labelStyle.Normal);
        float labelWidth = labelSize.X + labelMargin * 2;
        var labelOffset = new Vector2(labelMargin, 0);
        var labelBounds = new Rect(currentX, nodeRowStartPos.Y, labelWidth, style.RowHeight);
        if (StatelessButton(labelId, labelBounds, node.Text, labelStyle, labelTextAlignment, DirectUI.Button.ActionMode.Press, textOffset: labelOffset))
        {
            clickedNode = node;
        }
        currentX += labelWidth;

        Context.AdvanceLayout(new Vector2((currentX - nodeRowStartPos.X), style.RowHeight));

        if (node.IsExpanded && node.Children.Count > 0)
        {
            for (int childIdx = 0; childIdx < node.Children.Count; childIdx++)
            {
                bool isLastChild = childIdx == node.Children.Count - 1;
                treeState.IndentLineState.Push(!isLastChild);
                ProcessTreeNodeRecursive(toggleId, childIdx, node.Children[childIdx], ref clickedNode);
                treeState.IndentLineState.Pop();
            }
        }
    }

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