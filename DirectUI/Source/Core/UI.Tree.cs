using System;
using System.Numerics;
using Vortice.Mathematics;

namespace DirectUI;

public static partial class UI
{
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

        var startLayoutPos = Context.Layout.GetCurrentPosition();
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

        Context.Layout.AdvanceLayout(new Vector2((currentX - nodeRowStartPos.X), style.RowHeight));

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