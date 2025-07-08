using System.Numerics;
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