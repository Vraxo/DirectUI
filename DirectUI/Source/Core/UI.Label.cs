using System.Net.NetworkInformation;
using System;
using System.Numerics;
using SkiaSharp;
using Vortice.Mathematics;
using Vulkan.Xlib;

namespace DirectUI;

public static partial class UI
{
    public static void Text(string id, string text, Vector2? size = null, ButtonStyle? style = null, Alignment? textAlignment = null)
    {
        if (!IsContextValid() || string.IsNullOrEmpty(text))
        {
            return;
        }
        var scale = Context.UIScale;
        var logicalSize = size ?? Vector2.Zero;

        ButtonStyle finalStyle = style ?? new();
        finalStyle.FontColor = GetStyleColor(StyleColor.Text, finalStyle.FontColor);

        Alignment finalAlignment = textAlignment
            ?? new(HAlignment.Left, VAlignment.Center);

        var measurementStyle = new ButtonStyle(finalStyle) { FontSize = finalStyle.FontSize * scale };
        Vector2 measuredSize = Context.TextService.MeasureText(text, measurementStyle) / scale; // unscale to get logical size

        Vector2 finalLogicalSize;
        if (size.HasValue)
        {
            finalLogicalSize = new Vector2(
                logicalSize.X > 0 ? logicalSize.X : measuredSize.X,
                logicalSize.Y > 0 ? logicalSize.Y : measuredSize.Y
            );
        }
        else
        {
            finalLogicalSize = measuredSize;
        }

        Vector2 finalPhysicalSize = finalLogicalSize * scale;
        Vector2 drawPos = Context.Layout.ApplyLayout(Vector2.Zero);

        // New: Automatically adjust vertical position for HBox alignment
        if (Context.Layout.IsInLayoutContainer() && Context.Layout.PeekContainer() is HBoxContainerState hbox)
        {
            if (hbox.VerticalAlignment != VAlignment.Top && hbox.FixedRowHeight.HasValue)
            {
                float yOffset = 0;
                switch (hbox.VerticalAlignment)
                {
                    case VAlignment.Center:
                        yOffset = (hbox.FixedRowHeight.Value - finalLogicalSize.Y) / 2f;
                        break;
                    case VAlignment.Bottom:
                        yOffset = hbox.FixedRowHeight.Value - finalLogicalSize.Y;
                        break;
                }
                drawPos.Y += yOffset * scale;
            }
        }

        Rect widgetBounds = new(drawPos.X, drawPos.Y, finalPhysicalSize.X, finalPhysicalSize.Y);

        if (!Context.Layout.IsRectVisible(widgetBounds))
        {
            Context.Layout.AdvanceLayout(finalLogicalSize);
            return;
        }

        var renderStyle = new ButtonStyle(finalStyle) { FontSize = finalStyle.FontSize * scale };

        DrawTextPrimitive(
            widgetBounds,
            text,
            renderStyle,
            finalAlignment,
            Vector2.Zero);

        Context.Layout.AdvanceLayout(finalLogicalSize);
    }

    /// <summary>
    /// Displays text that wraps automatically based on the specified width.
    /// The layout height is determined by the content.
    /// </summary>
    public static void WrappedText(string id, string text, Vector2 size, ButtonStyle? style = null, Alignment? textAlignment = null)
    {
        if (!IsContextValid())
        {
            // Still advance layout if a fixed height was given
            if (size.Y > 0) Context.Layout.AdvanceLayout(size);
            return;
        }

        if (string.IsNullOrEmpty(text))
        {
            Context.Layout.AdvanceLayout(new Vector2(size.X, 0));
            return;
        }

        var scale = Context.UIScale;
        var logicalSize = size;

        ButtonStyle finalStyle = style ?? new ButtonStyle();
        finalStyle.FontColor = GetStyleColor(StyleColor.Text, finalStyle.FontColor);

        // Wrapped text should default to top alignment for natural reading flow
        Alignment finalAlignment = textAlignment ?? new(HAlignment.Left, VAlignment.Top);

        var renderStyle = new ButtonStyle(finalStyle) { FontSize = finalStyle.FontSize * scale };

        // Get a text layout that is constrained by the available width.
        // The height constraint is set to max to allow it to grow and wrap naturally.
        var textLayout = Context.TextService.GetTextLayout(text, renderStyle, new Vector2(logicalSize.X * scale, float.MaxValue), finalAlignment);

        if (textLayout is null)
        {
            if (logicalSize.Y > 0) Context.Layout.AdvanceLayout(logicalSize);
            return;
        }

        // The actual content size is determined by the layout's metrics.
        Vector2 actualPhysicalContentSize = textLayout.Size;
        Vector2 actualLogicalContentSize = actualPhysicalContentSize / scale;

        // The final size for layout advancement is the constrained width and the actual measured height.
        Vector2 finalLogicalSize = new Vector2(logicalSize.X, actualLogicalContentSize.Y);
        Vector2 finalPhysicalSize = finalLogicalSize * scale;

        Vector2 drawPos = Context.Layout.ApplyLayout(Vector2.Zero);
        Rect widgetBounds = new(drawPos.X, drawPos.Y, finalPhysicalSize.X, finalPhysicalSize.Y);

        if (!Context.Layout.IsRectVisible(widgetBounds))
        {
            Context.Layout.AdvanceLayout(finalLogicalSize);
            return;
        }

        DrawTextPrimitive(
            widgetBounds,
            text,
            renderStyle,
            finalAlignment,
            Vector2.Zero);

        Context.Layout.AdvanceLayout(finalLogicalSize);
    }
}