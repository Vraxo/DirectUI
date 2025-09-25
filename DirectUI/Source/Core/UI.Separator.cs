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

        var scale = Context.UIScale;

        // All input parameters are logical
        float logicalWidth = width;
        float logicalThickness = thickness;
        float logicalPadding = verticalPadding;

        Color4 finalColor = color ?? DefaultTheme.NormalBorder;
        float logicalTotalHeight = logicalThickness + (logicalPadding * 2);
        Vector2 logicalSize = new(logicalWidth, logicalTotalHeight);

        // Get the physical top-left position for drawing
        Vector2 physicalDrawPos = Context.Layout.ApplyLayout(Vector2.Zero);
        Vector2 physicalSize = logicalSize * scale;

        Rect physicalWidgetBounds = new(physicalDrawPos.X, physicalDrawPos.Y, physicalSize.X, physicalSize.Y);

        if (!Context.Layout.IsRectVisible(physicalWidgetBounds))
        {
            Context.Layout.AdvanceLayout(logicalSize);
            return;
        }

        // Calculate physical line properties for rendering
        float physicalThickness = logicalThickness * scale;
        float physicalPadding = logicalPadding * scale;

        float lineY = physicalDrawPos.Y + physicalPadding + (physicalThickness * 0.5f);
        Vector2 lineStart = new(physicalDrawPos.X, lineY);
        Vector2 lineEnd = new(physicalDrawPos.X + physicalSize.X, lineY);

        // Use the renderer to draw the line with physical values
        Context.Renderer.DrawLine(lineStart, lineEnd, finalColor, physicalThickness);

        // Advance the layout using the logical size
        Context.Layout.AdvanceLayout(logicalSize);
    }
}