using System;
using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Mathematics;
using D2D = Vortice.Direct2D1;

namespace DirectUI;

public static partial class UI
{
    // --- Widgets ---
    public static bool Button(string id, ButtonDefinition definition)
    {
        if (!IsContextValid() || definition is null) return false;
        Button buttonInstance = GetOrCreateElement<Button>(id);
        Vector2 elementPosition = ApplyLayout(definition.Position);
        buttonInstance.Position = elementPosition;
        ApplyButtonDefinition(buttonInstance, definition);

        bool pushedClip = false;
        Rect cellClipRect = Rect.Empty;
        if (IsInLayoutContainer())
        {
            if (containerStack.Peek() is GridContainerState grid)
            {
                float clipStartY = grid.CurrentDrawPosition.Y; float gridBottomY = grid.StartPosition.Y + grid.AvailableSize.Y;
                float clipHeight = Math.Max(0f, gridBottomY - clipStartY);
                cellClipRect = new Rect(grid.CurrentDrawPosition.X, clipStartY, Math.Max(0f, grid.CellWidth), clipHeight);
                if (CurrentRenderTarget is not null && cellClipRect.Width > 0 && cellClipRect.Height > 0)
                { CurrentRenderTarget.PushAxisAlignedClip(cellClipRect, D2D.AntialiasMode.Aliased); pushedClip = true; }
            }
        }

        bool clicked = buttonInstance.Update(id);
        if (pushedClip && CurrentRenderTarget is not null)
        { CurrentRenderTarget.PopAxisAlignedClip(); }

        AdvanceLayout(buttonInstance.Size);
        return clicked;
    }

    public static float HSlider(string id, float currentValue, SliderDefinition definition)
    {
        if (!IsContextValid() || definition is null) return currentValue;
        InternalHSliderLogic sliderInstance = GetOrCreateElement<InternalHSliderLogic>(id);
        Vector2 elementPosition = ApplyLayout(definition.Position);
        sliderInstance.Position = elementPosition;
        ApplySliderDefinition(sliderInstance, definition);
        sliderInstance.Direction = definition.HorizontalDirection;

        bool pushedClip = false;
        Rect cellClipRect = Rect.Empty;
        if (IsInLayoutContainer())
        {
            if (containerStack.Peek() is GridContainerState grid)
            {
                float clipStartY = grid.CurrentDrawPosition.Y; float gridBottomY = grid.StartPosition.Y + grid.AvailableSize.Y;
                float clipHeight = Math.Max(0f, gridBottomY - clipStartY);
                cellClipRect = new Rect(grid.CurrentDrawPosition.X, clipStartY, Math.Max(0f, grid.CellWidth), clipHeight);
                if (CurrentRenderTarget is not null && cellClipRect.Width > 0 && cellClipRect.Height > 0)
                { CurrentRenderTarget.PushAxisAlignedClip(cellClipRect, D2D.AntialiasMode.Aliased); pushedClip = true; }
            }
        }

        float newValue = sliderInstance.UpdateAndDraw(id, CurrentInputState, GetCurrentDrawingContext(), currentValue);
        if (pushedClip && CurrentRenderTarget is not null)
        { CurrentRenderTarget.PopAxisAlignedClip(); }

        AdvanceLayout(sliderInstance.Size);
        return newValue;
    }

    public static float VSlider(string id, float currentValue, SliderDefinition definition)
    {
        if (!IsContextValid() || definition is null) return currentValue;
        InternalVSliderLogic sliderInstance = GetOrCreateElement<InternalVSliderLogic>(id);
        Vector2 elementPosition = ApplyLayout(definition.Position);
        sliderInstance.Position = elementPosition;
        ApplySliderDefinition(sliderInstance, definition);
        sliderInstance.Direction = definition.VerticalDirection;

        bool pushedClip = false;
        Rect cellClipRect = Rect.Empty;
        if (IsInLayoutContainer())
        {
            if (containerStack.Peek() is GridContainerState grid)
            {
                float clipStartY = grid.CurrentDrawPosition.Y; float gridBottomY = grid.StartPosition.Y + grid.AvailableSize.Y;
                float clipHeight = Math.Max(0f, gridBottomY - clipStartY);
                cellClipRect = new Rect(grid.CurrentDrawPosition.X, clipStartY, Math.Max(0f, grid.CellWidth), clipHeight);
                if (CurrentRenderTarget is not null && cellClipRect.Width > 0 && cellClipRect.Height > 0)
                { CurrentRenderTarget.PushAxisAlignedClip(cellClipRect, D2D.AntialiasMode.Aliased); pushedClip = true; }
            }
        }

        float newValue = sliderInstance.UpdateAndDraw(id, CurrentInputState, GetCurrentDrawingContext(), currentValue);
        if (pushedClip && CurrentRenderTarget is not null)
        { CurrentRenderTarget.PopAxisAlignedClip(); }

        AdvanceLayout(sliderInstance.Size);

        return newValue;
    }

    public static void Tree<T>(string id, TreeNode<T> root, out TreeNode<T>? clickedNode, TreeStyle? style = null)
    {
        if (!IsContextValid() || root is null) { clickedNode = null; return; }

        clickedNode = null;
        var treeStyle = style ?? GetOrCreateElement<TreeStyle>(id + "_style");

        BeginTree(id, treeStyle);
        ProcessTreeNodeRecursive(id, 0, root, ref clickedNode);
        EndTree();
    }

    private static void ProcessTreeNodeRecursive<T>(string parentId, int index, TreeNode<T> node, ref TreeNode<T>? clickedNode)
    {
        if (treeStateStack.Count == 0) return;
        var treeState = treeStateStack.Peek();
        var style = treeState.Style;
        var renderTarget = CurrentRenderTarget!;

        // --- Get current position and draw hierarchy lines ---
        var startLayoutPos = GetCurrentLayoutPositionInternal();
        var brush = GetOrCreateBrush(style.LineColor);
        if (brush is not null)
        {
            // Draw vertical lines for prior indent levels
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

            // Draw horizontal line for the current node itself
            if (treeState.IndentLineState.Count > 0)
            {
                float hLineXStart = startLayoutPos.X + ((treeState.IndentLineState.Count - 1) * style.Indent) + (style.Indent * 0.5f);
                float hLineY = startLayoutPos.Y + style.RowHeight * 0.5f;
                renderTarget.DrawLine(new Vector2(hLineXStart, hLineY), new Vector2(hLineXStart + style.Indent * 0.5f, hLineY), brush, 1.0f);
            }
        }

        // --- Manually lay out the node row ---
        float indentSize = treeState.IndentLineState.Count * style.Indent;
        string currentId = $"{parentId}_{index}_{node.Text.GetHashCode()}";
        var nodeRowStartPos = startLayoutPos + new Vector2(indentSize, 0);
        float currentX = nodeRowStartPos.X;
        float gap = 5;

        // --- Toggle Button ---
        float toggleWidth = style.RowHeight - 4;
        if (node.Children.Count > 0)
        {
            var bounds = new Rect(currentX, nodeRowStartPos.Y, toggleWidth, style.RowHeight);
            if (StatelessButton(currentId + "_toggle", bounds, node.IsExpanded ? "-" : "+", style.ToggleStyle, new Alignment(HAlignment.Center, VAlignment.Center), DirectUI.Button.ActionMode.Release))
            {
                node.IsExpanded = !node.IsExpanded;
            }
        }
        currentX += toggleWidth;

        // --- Label Button ---
        currentX += gap; // Add gap before the label
        var labelStyle = style.NodeLabelStyle;
        var labelTextAlignment = new Alignment(HAlignment.Left, VAlignment.Center);
        float labelMargin = 4;
        var labelSize = MeasureText(CurrentDWriteFactory!, node.Text, labelStyle.Normal);
        float labelWidth = labelSize.X + labelMargin * 2;
        var labelOffset = new Vector2(labelMargin, 0);

        var labelBounds = new Rect(currentX, nodeRowStartPos.Y, labelWidth, style.RowHeight);
        if (StatelessButton(currentId + "_label", labelBounds, node.Text, labelStyle, labelTextAlignment, DirectUI.Button.ActionMode.Press, textOffset: labelOffset))
        {
            clickedNode = node;
        }
        currentX += labelWidth;

        // --- Advance parent layout ---
        float totalWidth = (currentX - nodeRowStartPos.X);
        AdvanceLayout(new Vector2(totalWidth, style.RowHeight));

        // --- Recurse if expanded ---
        if (node.IsExpanded && node.Children.Count > 0)
        {
            for (int childIdx = 0; childIdx < node.Children.Count; childIdx++)
            {
                bool isLastChild = childIdx == node.Children.Count - 1;
                treeState.IndentLineState.Push(!isLastChild); // Don't draw vertical line past the last child
                ProcessTreeNodeRecursive(currentId, childIdx, node.Children[childIdx], ref clickedNode);
                treeState.IndentLineState.Pop();
            }
        }
    }
}