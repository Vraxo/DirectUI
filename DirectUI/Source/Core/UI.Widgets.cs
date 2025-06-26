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

        // --- Layout the node row using a temporary HBox ---
        float indentSize = treeState.IndentLineState.Count * style.Indent;
        string currentId = $"{parentId}_{index}_{node.Text.GetHashCode()}";
        BeginHBoxContainer(currentId + "_row", startLayoutPos + new Vector2(indentSize, 0), 5);

        // --- Toggle Button ---
        if (node.Children.Count > 0)
        {
            var toggleDef = new ButtonDefinition
            {
                Text = node.IsExpanded ? "-" : "+",
                Theme = style.ToggleStyle,
                Size = new Vector2(style.RowHeight - 4, style.RowHeight),
                TextAlignment = new Alignment(HAlignment.Center, VAlignment.Center)
            };
            if (Button(currentId + "_toggle", toggleDef))
            {
                node.IsExpanded = !node.IsExpanded;
            }
        }
        else
        {
            AdvanceLayout(new Vector2(style.RowHeight - 4, 0)); // Spacer to align labels
        }

        // --- Label Button ---
        var labelDef = new ButtonDefinition
        {
            Text = node.Text,
            Theme = style.NodeLabelStyle,
            AutoWidth = true,
            Size = new Vector2(0, style.RowHeight),
            TextAlignment = new Alignment(HAlignment.Left, VAlignment.Center),
            TextMargin = new Vector2(4, 0),
            LeftClickActionMode = DirectUI.Button.ActionMode.Press
        };
        if (Button(currentId + "_label", labelDef))
        {
            clickedNode = node;
        }

        EndHBoxContainer(); // This will advance the parent container's layout cursor vertically

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