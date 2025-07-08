using System.Numerics;
using Vortice.Mathematics;

namespace DirectUI;

public static partial class UI
{
    public static void Separator(float width, float thickness = 1f, float verticalPadding = 4f, Color4? color = null)
    {
        if (!IsContextValid())
        {
            return;
        }

        Color4 finalColor = color ?? DefaultTheme.NormalBorder;
        float totalHeight = thickness + (verticalPadding * 2);
        Vector2 size = new(width, totalHeight);

        Vector2 drawPos = Context.Layout.GetCurrentPosition();
        Rect widgetBounds = new(drawPos.X, drawPos.Y, size.X, size.Y);

        if (!Context.Layout.IsRectVisible(widgetBounds))
        {
            Context.Layout.AdvanceLayout(size);
            return;
        }

        float lineY = drawPos.Y + verticalPadding + (thickness * 0.5f);
        Vector2 lineStart = new(drawPos.X, lineY);
        Vector2 lineEnd = new(drawPos.X + width, lineY);

        // Use the renderer to get a brush and draw the line
        var brush = Context.Renderer.GetOrCreateBrush(finalColor);

        if (brush != null)
        {
            Context.Renderer.DrawLine(lineStart, lineEnd, finalColor, thickness);
        }

        Context.Layout.AdvanceLayout(size);
    }
}