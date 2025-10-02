using System;
using System.Globalization;
using System.Numerics;
using DirectUI.Core;
using SkiaSharp;
using SkiaSharp.HarfBuzz;
using Typography.OpenFont;

namespace DirectUI.Backends.SkiaSharp;

internal class SilkNetTextLayout : ITextLayout
{
    public Vector2 Size { get; }
    public string Text { get; }
    private readonly SKTypeface _typeface;
    private readonly ButtonStyle _style;

    public SilkNetTextLayout(string text, SKTypeface typeface, ButtonStyle style)
    {
        Text = text;
        _typeface = typeface;
        _style = style;
        using var font = new SKFont(typeface, style.FontSize);
        using var paint = new SKPaint(font) { IsAntialias = true };
        using var shaper = new SKShaper(typeface);

        var shapeResult = shaper.Shape(text, paint);
        var width = shapeResult.Width;

        var fontMetrics = paint.FontMetrics;
        var height = fontMetrics.Descent - fontMetrics.Ascent;

        Size = new Vector2(width, height);
    }
    public TextHitTestMetrics HitTestTextPosition(int textPosition, bool isTrailingHit)
    {
        if (string.IsNullOrEmpty(Text) && textPosition == 0)
        {
            return new TextHitTestMetrics(Vector2.Zero, new Vector2(1, Size.Y));
        }
        textPosition = Math.Clamp(textPosition, 0, Text.Length);

        using var font = new SKFont(_typeface, _style.FontSize);
        using var paint = new SKPaint(font);
        using var shaper = new SKShaper(_typeface);

        // Shape the substring up to the character position to get its width, which is the caret's X coordinate.
        string sub = Text[..textPosition];
        float x = shaper.Shape(sub, paint).Width;

        // The width of the caret itself can be 1 pixel. For selection highlighting, we need the grapheme width.
        float graphemeWidth = paint.MeasureText(" ");

        if (textPosition < Text.Length)
        {
            // Find the grapheme at the current position to get a more accurate width.
            var enumerator = StringInfo.GetTextElementEnumerator(Text, textPosition);
            if (enumerator.MoveNext())
            {
                string grapheme = enumerator.GetTextElement();
                if (!string.IsNullOrEmpty(grapheme))
                {
                    graphemeWidth = shaper.Shape(grapheme, paint).Width;
                }
            }
        }

        return new TextHitTestMetrics(new Vector2(x, 0), new Vector2(graphemeWidth, Size.Y));
    }

    public TextHitTestResult HitTestPoint(Vector2 point)
    {
        if (string.IsNullOrEmpty(Text))
        {
            return new TextHitTestResult(0, false, false, new TextHitTestMetrics(Vector2.Zero, Vector2.Zero));
        }

        using var font = new SKFont(_typeface, _style.FontSize);
        using var paint = new SKPaint(font);
        using var shaper = new SKShaper(_typeface);

        bool isInside = point.X >= 0 && point.X <= Size.X && point.Y >= 0 && point.Y <= Size.Y;

        var enumerator = StringInfo.GetTextElementEnumerator(Text);
        float currentX = 0;

        while (enumerator.MoveNext())
        {
            string grapheme = enumerator.GetTextElement();
            float graphemeWidth = shaper.Shape(grapheme, paint).Width;
            float graphemeMidPoint = currentX + graphemeWidth / 2f;

            // Check if the click is within the current grapheme's horizontal space
            if (point.X < currentX + graphemeWidth)
            {
                bool isTrailingHit = point.X > graphemeMidPoint;
                int charIndex = isTrailingHit ? enumerator.ElementIndex + grapheme.Length : enumerator.ElementIndex;

                var metrics = HitTestTextPosition(enumerator.ElementIndex, false); // Metrics of the grapheme itself

                return new TextHitTestResult(charIndex, isTrailingHit, isInside, metrics);
            }

            currentX += graphemeWidth;
        }

        // If the click is past all graphemes, place the caret at the end.
        return new TextHitTestResult(Text.Length, false, isInside, HitTestTextPosition(Text.Length, false));
    }

    public void Dispose()
    {
        // This class doesn't own unmanaged resources directly
    }
}