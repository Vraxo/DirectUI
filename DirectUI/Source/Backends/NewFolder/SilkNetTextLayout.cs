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
    private readonly SKTypeface _typeface;
    private readonly ButtonStyle _style;

    public SilkNetTextLayout(string text, SKTypeface typeface, ButtonStyle style)
    {
        Text = text;
        _typeface = typeface;
        _style = style;
        using var font = new SKFont(typeface, style.FontSize);
        using var paint = new SKPaint(font);
        var rect = new SKRect();
        paint.MeasureText(text, ref rect);
        Size = new Vector2(rect.Width, rect.Height);
    }
    public TextHitTestMetrics HitTestTextPosition(int textPosition, bool isTrailingHit)
    {
        if (string.IsNullOrEmpty(Text) && textPosition == 0)
        {
            return new TextHitTestMetrics(Vector2.Zero, new Vector2(1, Size.Y)); // Default caret for empty string
        }
        textPosition = Math.Clamp(textPosition, 0, Text.Length);

        using var font = new SKFont(_typeface, _style.FontSize);
        using var paint = new SKPaint(font);

        // Measure the substring up to the caret position to get the X coordinate.
        string sub = Text[..textPosition];
        float x = paint.MeasureText(sub);

        // Measure the width of the character at the caret position to determine its size.
        float charWidth;
        if (textPosition < Text.Length)
        {
            charWidth = paint.MeasureText(Text[textPosition].ToString());
        }
        else // Caret is at the end of the string.
        {
            charWidth = Text.Length > 0 ? paint.MeasureText(Text[^1].ToString()) : 10f;
        }

        // isTrailingHit is a concept for converting a point to an index,
        // but here we are converting an index to a point, so we just use the calculated x.
        return new TextHitTestMetrics(new Vector2(x, 0), new Vector2(charWidth, Size.Y));
    }

    public TextHitTestResult HitTestPoint(Vector2 point)
    {
        if (string.IsNullOrEmpty(Text))
        {
            return new TextHitTestResult(0, false, false, new TextHitTestMetrics(Vector2.Zero, Vector2.Zero));
        }

        using var font = new SKFont(_typeface, _style.FontSize);
        using var paint = new SKPaint(font);

        bool isInside = point.X >= 0 && point.X <= Size.X && point.Y >= 0 && point.Y <= Size.Y;

        // Use BreakText to find the character index that corresponds to the click's X coordinate.
        int textPosition = (int)paint.BreakText(Text, point.X);
        textPosition = Math.Clamp(textPosition, 0, Text.Length);

        // Determine if it's a "trailing hit" by checking which side of the character's midpoint the click landed on.
        float charStartPos = paint.MeasureText(Text[..textPosition]);
        float charEndPos = (textPosition < Text.Length)
            ? charStartPos + paint.MeasureText(Text[textPosition].ToString())
            : charStartPos;

        bool isTrailingHit = (point.X - charStartPos) > ((charEndPos - charStartPos) / 2f);

        // Get metrics for the hit character.
        var metrics = HitTestTextPosition(textPosition, false);

        return new TextHitTestResult(textPosition, isTrailingHit, isInside, metrics);
    }
    public void Dispose()
    {
        // This class doesn't own unmanaged resources directly
    }
}