using System;
using System.Collections.Generic;
using System.Numerics;
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

    public static bool TabButton(string id, string text, bool isActive, TabStylePack? theme = null, bool autoWidth = true, bool disabled = false)
    {
        if (!IsContextValid()) return false;

        var intId = id.GetHashCode();
        var tabTheme = theme ?? State.GetOrCreateElement<TabStylePack>(id + "_theme_default");

        var position = Context.GetCurrentLayoutPosition();
        var textMargin = new Vector2(15, 5);

        // A consistent tab bar height is assumed for now.
        float tabBarHeight = 30f;
        Vector2 tabSize;

        if (autoWidth)
        {
            // All tabs should have same size based on normal state for layout consistency
            var styleForMeasuring = tabTheme.Normal;
            Vector2 measuredSize = Resources.MeasureText(Context.DWriteFactory, text, styleForMeasuring);
            tabSize = new Vector2(measuredSize.X + textMargin.X * 2, tabBarHeight);
        }
        else
        {
            // This would require size information from the container, not supported yet for HBox.
            // For now, we rely on autoWidth.
            tabSize = new Vector2(100, tabBarHeight);
        }

        Rect bounds = new(position.X, position.Y, tabSize.X, tabSize.Y);
        InputState input = Context.InputState;
        bool isHovering = !disabled && bounds.Contains(input.MousePosition.X, input.MousePosition.Y);
        bool wasClicked = false;

        if (isHovering) State.SetPotentialInputTarget(intId);

        if (input.WasLeftMousePressedThisFrame && isHovering && State.PotentialInputTargetId == intId)
        {
            wasClicked = true;
        }

        tabTheme.UpdateCurrentStyle(isHovering, isActive, disabled);

        // Drawing
        var rt = Context.RenderTarget;
        Resources.DrawBoxStyleHelper(rt, new Vector2(bounds.X, bounds.Y), new Vector2(bounds.Width, bounds.Height), tabTheme.Current);

        // Draw Text
        var textBrush = Resources.GetOrCreateBrush(rt, tabTheme.Current.FontColor);
        var textFormat = Resources.GetOrCreateTextFormat(Context.DWriteFactory, tabTheme.Current);
        if (textBrush is not null && textFormat is not null && !string.IsNullOrEmpty(text))
        {
            textFormat.TextAlignment = Vortice.DirectWrite.TextAlignment.Center;
            textFormat.ParagraphAlignment = ParagraphAlignment.Center;
            rt.DrawText(text, textFormat, bounds, textBrush);
        }

        Context.AdvanceLayout(tabSize);
        return wasClicked;
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
}