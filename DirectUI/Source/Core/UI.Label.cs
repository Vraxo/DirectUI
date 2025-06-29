using System.Numerics;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;

namespace DirectUI;

public static partial class UI
{
    public static void Label(string id, string text, Vector2? size = null, ButtonStyle? style = null, Alignment? textAlignment = null)
    {
        if (!IsContextValid() || string.IsNullOrEmpty(text))
        {
            return;
        }

        ButtonStyle finalStyle = style ?? new();
        finalStyle.FontColor = GetStyleColor(StyleColor.Text, finalStyle.FontColor);

        Alignment finalAlignment = textAlignment
            ?? new(HAlignment.Left, VAlignment.Center);

        Vector2 measuredSize = Resources.MeasureText(Context.DWriteFactory, text, finalStyle);

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
        Rect widgetBounds = new(drawPos.X, drawPos.Y, finalSize.X, finalSize.Y);

        if (!Context.Layout.IsRectVisible(widgetBounds))
        {
            Context.Layout.AdvanceLayout(finalSize);
            return;
        }

        DrawLabelText(
            Context.RenderTarget,
            Context.DWriteFactory,
            Resources,
            widgetBounds,
            text,
            finalStyle,
            finalAlignment);

        Context.Layout.AdvanceLayout(finalSize);
    }

    private static void DrawLabelText(
        ID2D1RenderTarget renderTarget,
        IDWriteFactory dwriteFactory,
        UIResources resources,
        Rect bounds,
        string text,
        ButtonStyle style,
        Alignment textAlignment)
    {
        ID2D1SolidColorBrush? textBrush = resources.GetOrCreateBrush(renderTarget, style.FontColor);

        if (textBrush is null)
        {
            return;
        }

        UIResources.TextLayoutCacheKey layoutKey = new(text, style, new(bounds.Width, bounds.Height), textAlignment);

        if (!resources.textLayoutCache.TryGetValue(layoutKey, out var textLayout))
        {
            IDWriteTextFormat? textFormat = resources.GetOrCreateTextFormat(dwriteFactory, style);

            if (textFormat is null)
            {
                return;
            }

            textLayout = dwriteFactory.CreateTextLayout(text, textFormat, bounds.Width, bounds.Height);
            textLayout.TextAlignment = textAlignment.Horizontal switch
            {
                HAlignment.Left => TextAlignment.Leading,
                HAlignment.Center => TextAlignment.Center,
                HAlignment.Right => TextAlignment.Trailing,
                _ => TextAlignment.Leading
            };
            textLayout.ParagraphAlignment = textAlignment.Vertical switch
            {
                VAlignment.Top => ParagraphAlignment.Near,
                VAlignment.Center => ParagraphAlignment.Center,
                VAlignment.Bottom => ParagraphAlignment.Far,
                _ => ParagraphAlignment.Near
            };
            resources.textLayoutCache[layoutKey] = textLayout;
        }

        float yOffsetCorrection = textAlignment.Vertical == VAlignment.Center
            ? -1.5f
            : 0f;

        renderTarget.DrawTextLayout(
            new(bounds.X, bounds.Y + yOffsetCorrection),
            textLayout,
            textBrush,
            DrawTextOptions.None);
    }
}