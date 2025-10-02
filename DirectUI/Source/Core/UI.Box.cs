using System.Numerics;
using Vortice.Mathematics;

namespace DirectUI;

public static partial class UI
{
    /// <summary>
    /// Draws a simple styled box.
    /// </summary>
    /// <param name="id">A unique identifier for the box.</param>
    /// <param name="size">The logical size of the box.</param>
    /// <param name="style">The style to apply to the box.</param>
    public static void Box(string id, Vector2 size, BoxStyle? style = null)
    {
        if (!IsContextValid()) return;

        var context = Context;
        var scale = context.UIScale;

        var logicalSize = size;
        var physicalSize = logicalSize * scale;
        var drawPos = context.Layout.ApplyLayout(Vector2.Zero);

        // New: Automatically adjust vertical position for HBox alignment
        if (Context.Layout.IsInLayoutContainer() && Context.Layout.PeekContainer() is HBoxContainerState hbox)
        {
            if (hbox.VerticalAlignment != VAlignment.Top && hbox.FixedRowHeight.HasValue)
            {
                float yOffset = 0;
                switch (hbox.VerticalAlignment)
                {
                    case VAlignment.Center:
                        yOffset = (hbox.FixedRowHeight.Value - logicalSize.Y) / 2f;
                        break;
                    case VAlignment.Bottom:
                        yOffset = hbox.FixedRowHeight.Value - logicalSize.Y;
                        break;
                }
                drawPos.Y += yOffset * scale;
            }
        }

        var bounds = new Rect(drawPos.X, drawPos.Y, physicalSize.X, physicalSize.Y);

        if (!context.Layout.IsRectVisible(bounds))
        {
            context.Layout.AdvanceLayout(logicalSize);
            return;
        }

        var finalStyle = style ?? new BoxStyle();
        context.Renderer.DrawBox(bounds, finalStyle);

        context.Layout.AdvanceLayout(logicalSize);
    }
}