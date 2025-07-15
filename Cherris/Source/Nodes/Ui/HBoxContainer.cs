using System.Linq;
using System.Numerics;

namespace Cherris;

public class HBoxContainer : Node2D
{
    public float Separation { get; set; } = 4f;
    public HAlignment ContentHAlignment { get; set; } = HAlignment.Center;

    public override void Process()
    {
        base.Process();
        UpdateLayout();
    }

    protected override Vector2 ComputeAutoSize()
    {
        var visibleNode2DChildren = Children.OfType<Node2D>().Where(c => c.Visible).ToList();
        float requiredWidth = 0;
        float maxHeight = 0;

        if (visibleNode2DChildren.Any())
        {
            foreach (Node2D child in visibleNode2DChildren)
            {
                requiredWidth += child.Size.X;
                maxHeight = Math.Max(maxHeight, child.Size.Y);
            }
            requiredWidth += (visibleNode2DChildren.Count - 1) * Separation;
        }
        return new Vector2(requiredWidth, maxHeight);
    }

    private void UpdateLayout()
    {
        var visibleNode2DChildren = Children.OfType<Node2D>().Where(c => c.Visible).ToList();

        float containerWidth = this.Size.X;
        float containerHeight = this.Size.Y;

        float totalRequiredContentWidth = 0;
        if (visibleNode2DChildren.Any())
        {
            foreach (Node2D child in visibleNode2DChildren)
            {
                totalRequiredContentWidth += child.Size.X;
            }
            totalRequiredContentWidth += (visibleNode2DChildren.Count - 1) * Separation;
        }

        float visualLeftLocalX = 0f;
        if (this.HAlignment == HAlignment.Center)
        {
            visualLeftLocalX = -containerWidth / 2f;
        }
        else if (this.HAlignment == HAlignment.Right)
        {
            visualLeftLocalX = -containerWidth;
        }

        float childVisualStartX_local;
        switch (this.ContentHAlignment)
        {
            case HAlignment.Left:
                childVisualStartX_local = visualLeftLocalX;
                break;
            case HAlignment.Center:
                childVisualStartX_local = visualLeftLocalX + (containerWidth - totalRequiredContentWidth) / 2f;
                break;
            case HAlignment.Right:
                childVisualStartX_local = visualLeftLocalX + containerWidth - totalRequiredContentWidth;
                break;
            case HAlignment.None:
            default:
                childVisualStartX_local = visualLeftLocalX;
                break;
        }

        float currentVisualX = childVisualStartX_local;
        foreach (Node2D child in visibleNode2DChildren)
        {
            float visualTopLocalY = 0f;
            if (this.VAlignment == VAlignment.Center) visualTopLocalY = -containerHeight / 2f;
            else if (this.VAlignment == VAlignment.Bottom) visualTopLocalY = -containerHeight;

            float childVisualTargetY_local;            switch (child.VAlignment)
            {
                case VAlignment.Top:
                    childVisualTargetY_local = visualTopLocalY;
                    break;
                case VAlignment.Center:
                    childVisualTargetY_local = visualTopLocalY + (containerHeight - child.Size.Y) / 2f;
                    break;
                case VAlignment.Bottom:
                    childVisualTargetY_local = visualTopLocalY + containerHeight - child.Size.Y;
                    break;
                case VAlignment.None:
                default:
                    childVisualTargetY_local = visualTopLocalY;
                    break;
            }
            child.Position = new Vector2(currentVisualX + child.Origin.X, childVisualTargetY_local + child.Origin.Y);

            currentVisualX += child.Size.X;
            if (visibleNode2DChildren.IndexOf(child) < visibleNode2DChildren.Count - 1)
            {
                currentVisualX += Separation;
            }
        }
    }
}