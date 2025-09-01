using System;
using System.Numerics;
using DirectUI.Core;
using SkiaSharp;
using Typography.OpenFont;

namespace DirectUI.Backends.SkiaSharp;

internal class SilkNetTextLayout : ITextLayout
{
    public Vector2 Size { get; }
    public string Text { get; }

    public SilkNetTextLayout(string text, SKTypeface typeface, ButtonStyle style)
    {
        Text = text;
        using var font = new SKFont(typeface, style.FontSize);
        using var paint = new SKPaint(font);
        var rect = new SKRect();
        paint.MeasureText(text, ref rect);
        Size = new Vector2(rect.Width, rect.Height);
    }
    public TextHitTestMetrics HitTestTextPosition(int textPosition, bool isTrailingHit)
    {
        if (string.IsNullOrEmpty(Text)) return new TextHitTestMetrics(Vector2.Zero, Vector2.Zero);

        float approxCharWidth = Size.X / Math.Max(1, Text.Length);
        float x = textPosition * approxCharWidth;
        float width = approxCharWidth;

        if (isTrailingHit) x += approxCharWidth;
        x = Math.Clamp(x, 0, Size.X);

        return new TextHitTestMetrics(new Vector2(x, 0), new Vector2(width, Size.Y));
    }

    public TextHitTestResult HitTestPoint(Vector2 point)
    {
        if (string.IsNullOrEmpty(Text))
        {
            return new TextHitTestResult(0, false, false, new TextHitTestMetrics(Vector2.Zero, Vector2.Zero));
        }

        bool isInside = point.X >= 0 && point.X <= Size.X && point.Y >= 0 && point.Y <= Size.Y;

        float approxCharWidth = (Text.Length > 0) ? Size.X / Text.Length : 0;
        int textPosition = 0;
        bool isTrailingHit = false;

        if (approxCharWidth > 0)
        {
            textPosition = (int)(point.X / approxCharWidth);
            isTrailingHit = (point.X % approxCharWidth) > (approxCharWidth / 2f);
        }

        textPosition = Math.Clamp(textPosition, 0, Text.Length);
        if (isTrailingHit && textPosition < Text.Length)
        {
            textPosition++;
        }
        textPosition = Math.Clamp(textPosition, 0, Text.Length);

        float hitMetricX = (textPosition == 0) ? 0 : (textPosition * approxCharWidth);
        if (isTrailingHit && textPosition > 0) hitMetricX -= approxCharWidth;

        return new TextHitTestResult(textPosition, isTrailingHit, isInside,
            new TextHitTestMetrics(new Vector2(hitMetricX, 0), new Vector2(approxCharWidth, Size.Y)));
    }
    public void Dispose()
    {
        // This class doesn't own unmanaged resources directly
    }
}