// DirectUI/Backends/Raylib/RaylibTextLayout.cs
using System;
using System.Numerics;
using DirectUI.Core;
using Vortice.Mathematics; // For Color4, Rect
using Raylib_cs; // Raylib specific library

namespace DirectUI.Backends;

/// <summary>
/// A Raylib-specific implementation of the ITextLayout interface.
/// This class stores text and provides approximate metrics.
/// </summary>
internal class RaylibTextLayout : ITextLayout
{
    public Vector2 Size { get; }
    public string Text { get; }

    private readonly Font _raylibFont;

    public RaylibTextLayout(string text, Font preloadedFont, float finalFontSize)
    {
        Text = text;
        _raylibFont = preloadedFont;

        // Measure using the provided final (compensated) font size.
        Size = Raylib.MeasureTextEx(_raylibFont, text, finalFontSize, finalFontSize / 10f);
    }

    public TextHitTestMetrics HitTestTextPosition(int textPosition, bool isTrailingHit)
    {
        // Raylib doesn't provide fine-grained text metrics per character easily.
        // This is a rough approximation based on overall text width.
        if (string.IsNullOrEmpty(Text)) return new TextHitTestMetrics(Vector2.Zero, Vector2.Zero);

        float approxCharWidth = Size.X / Text.Length;
        float x = textPosition * approxCharWidth;
        float width = approxCharWidth;

        // Adjust x if it's a trailing hit, to measure the position AFTER the character
        if (isTrailingHit) x += approxCharWidth;

        // Clamp to prevent out-of-bounds positions
        x = Math.Clamp(x, 0, Size.X);

        return new TextHitTestMetrics(new Vector2(x, 0), new Vector2(width, Size.Y));
    }

    public TextHitTestResult HitTestPoint(Vector2 point)
    {
        // This is a rough approximation for hit testing in Raylib
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

        // Caret position can be one past the last character (i.e., at the end of the string).
        textPosition = Math.Clamp(textPosition, 0, Text.Length);
        if (isTrailingHit && textPosition < Text.Length) // If it's a trailing hit and not at the very end
        {
            textPosition++;
        }
        textPosition = Math.Clamp(textPosition, 0, Text.Length); // Final clamp

        // Recalculate metrics for the specific hit position
        float hitMetricX = (textPosition == 0) ? 0 : (textPosition * approxCharWidth);
        if (isTrailingHit && textPosition > 0) hitMetricX -= approxCharWidth; // Adjust if hit is trailing edge

        return new TextHitTestResult(textPosition, isTrailingHit, isInside,
            new TextHitTestMetrics(new Vector2(hitMetricX, 0), new Vector2(approxCharWidth, Size.Y)));
    }

    public void Dispose()
    {
        // This object doesn't own the font resource, so it does not dispose it.
        // FontManager is responsible for unloading fonts.
    }
}