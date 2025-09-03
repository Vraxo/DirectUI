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
        using var paint = new SKPaint(font) { IsAntialias = true };

        // Use a robust measurement that includes trailing spaces
        float width = MeasureTextWithTrailingWhitespace(paint, text);
        var rect = new SKRect();
        // Measure normally to get an accurate height
        paint.MeasureText("Gg", ref rect); // Use characters with ascenders/descenders for stable height
        Size = new Vector2(width, rect.Height);
    }
    public TextHitTestMetrics HitTestTextPosition(int textPosition, bool isTrailingHit)
    {
        if (string.IsNullOrEmpty(Text) && textPosition == 0)
        {
            return new TextHitTestMetrics(Vector2.Zero, new Vector2(1, Size.Y)); // Default caret for empty string
        }
        textPosition = Math.Clamp(textPosition, 0, Text.Length);

        using var font = new SKFont(_typeface, _style.FontSize);
        using var paint = new SKPaint(font) { IsAntialias = true };

        // Measure the substring up to the caret position to get the X coordinate.
        string sub = Text[..textPosition];
        float x = MeasureTextWithTrailingWhitespace(paint, sub);

        // Measure the width of the character at the caret position to determine its size.
        float charWidth;
        if (textPosition < Text.Length)
        {
            // Use the robust measurement here as well in case the character is a space
            charWidth = MeasureTextWithTrailingWhitespace(paint, Text[textPosition].ToString());
        }
        else // Caret is at the end of the string.
        {
            // Use the width of a space as a sensible default for the caret after the last character.
            charWidth = paint.MeasureText(" ");
        }

        return new TextHitTestMetrics(new Vector2(x, 0), new Vector2(charWidth, Size.Y));
    }

    public TextHitTestResult HitTestPoint(Vector2 point)
    {
        if (string.IsNullOrEmpty(Text))
        {
            return new TextHitTestResult(0, false, false, new TextHitTestMetrics(Vector2.Zero, Vector2.Zero));
        }

        using var font = new SKFont(_typeface, _style.FontSize);
        using var paint = new SKPaint(font) { IsAntialias = true };

        bool isInside = point.X >= 0 && point.X <= Size.X && point.Y >= 0 && point.Y <= Size.Y;

        // Use BreakText to find the character index that corresponds to the click's X coordinate.
        // This is still a good starting point.
        int textPosition = (int)paint.BreakText(Text, point.X);
        textPosition = Math.Clamp(textPosition, 0, Text.Length);

        // Determine if it's a "trailing hit" by checking which side of the character's midpoint the click landed on.
        float charStartPos = MeasureTextWithTrailingWhitespace(paint, Text[..textPosition]);
        float charEndPos = (textPosition < Text.Length)
            ? charStartPos + MeasureTextWithTrailingWhitespace(paint, Text[textPosition].ToString())
            : charStartPos;

        bool isTrailingHit = (point.X - charStartPos) > ((charEndPos - charStartPos) / 2f);

        // Get metrics for the hit character.
        var metrics = HitTestTextPosition(textPosition, false);

        return new TextHitTestResult(textPosition, isTrailingHit, isInside, metrics);
    }

    /// <summary>
    /// Measures text width while reliably including trailing whitespace, which SKPaint.MeasureText often trims.
    /// </summary>
    private static float MeasureTextWithTrailingWhitespace(SKPaint paint, string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;

        string trimmedText = text.TrimEnd(' ');
        float width = paint.MeasureText(trimmedText);

        int trailingSpaceCount = text.Length - trimmedText.Length;
        if (trailingSpaceCount > 0)
        {
            // Measure a single space to get its width accurately for the current font.
            float spaceWidth = paint.MeasureText(" ");
            width += trailingSpaceCount * spaceWidth;
        }

        return width;
    }

    public void Dispose()
    {
        // This class doesn't own unmanaged resources directly
    }
}