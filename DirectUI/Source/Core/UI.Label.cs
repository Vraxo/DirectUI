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

        ButtonStyle finalStyle = style ?? new();
        finalStyle.FontColor = GetStyleColor(StyleColor.Text, finalStyle.FontColor);

        Alignment finalAlignment = textAlignment
            ?? new(HAlignment.Left, VAlignment.Center);

        Vector2 measuredSize = Context.TextService.MeasureText(text, finalStyle);

        Vector2 finalSize;
        if (size.HasValue)
        {
            finalSize = new Vector2(
                size.Value.X > 0 ? size.Value.X : measuredSize.X,
                size.Value.Y > 0 ? size.Value.Y : measuredSize.Y
            );
        }
        else
        {
            finalSize = measuredSize;
        }

        Vector2 drawPos = Context.Layout.GetCurrentPosition();

        // New: Automatically adjust vertical position for HBox alignment
        if (Context.Layout.IsInLayoutContainer() && Context.Layout.PeekContainer() is HBoxContainerState hbox)
        {
            if (hbox.VerticalAlignment != VAlignment.Top && hbox.FixedRowHeight.HasValue)
            {
                float yOffset = 0;
                switch (hbox.VerticalAlignment)
                {
                    case VAlignment.Center:
                        yOffset = (hbox.FixedRowHeight.Value - finalSize.Y) / 2f;
                        break;
                    case VAlignment.Bottom:
                        yOffset = hbox.FixedRowHeight.Value - finalSize.Y;
                        break;
                }
                drawPos.Y += yOffset;
            }
        }

        Rect widgetBounds = new(drawPos.X, drawPos.Y, finalSize.X, finalSize.Y);

        if (!Context.Layout.IsRectVisible(widgetBounds))
        {
            Context.Layout.AdvanceLayout(finalSize);
            return;
        }

        DrawTextPrimitive(
            widgetBounds,
            text,
            finalStyle,
            finalAlignment,
            Vector2.Zero);

        Context.Layout.AdvanceLayout(finalSize);
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

        ButtonStyle finalStyle = style ?? new ButtonStyle();
        finalStyle.FontColor = GetStyleColor(StyleColor.Text, finalStyle.FontColor);

        // Wrapped text should default to top alignment for natural reading flow
        Alignment finalAlignment = textAlignment ?? new(HAlignment.Left, VAlignment.Top);

        // Get a text layout that is constrained by the available width.
        // The height constraint is set to max to allow it to grow and wrap naturally.
        var textLayout = Context.TextService.GetTextLayout(text, finalStyle, new Vector2(size.X, float.MaxValue), finalAlignment);

        if (textLayout == null)
        {
            if (size.Y > 0) Context.Layout.AdvanceLayout(size);
            return;
        }

        // The actual content size is determined by the layout's metrics.
        Vector2 actualContentSize = textLayout.Size;

        // The final size for layout advancement is the constrained width and the actual measured height.
        Vector2 finalSize = new Vector2(size.X, actualContentSize.Y);

        Vector2 drawPos = Context.Layout.GetCurrentPosition();
        Rect widgetBounds = new(drawPos.X, drawPos.Y, finalSize.X, finalSize.Y);

        if (!Context.Layout.IsRectVisible(widgetBounds))
        {
            Context.Layout.AdvanceLayout(finalSize);
            return;
        }

        DrawTextPrimitive(
            widgetBounds,
            text,
            finalStyle,
            finalAlignment,
            Vector2.Zero);

        Context.Layout.AdvanceLayout(finalSize);
    }
}