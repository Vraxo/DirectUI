using System;
using System.Numerics;
using DirectUI.Core;
using static System.Net.Mime.MediaTypeNames;

namespace DirectUI.Backends.Vulkan;

/// <summary>
/// A Veldrid-specific implementation of the ITextLayout interface.
/// This class stores pre-measured text and provides approximate hit-testing.
/// </summary>
public class VeldridTextLayout : ITextLayout
{
    public Vector2 Size { get; }
    public string Text { get; }

    public VeldridTextLayout(string text, Vector2 size)
    {
        Text = text;
        Size = size;
    }

    public TextHitTestMetrics HitTestTextPosition(int textPosition, bool isTrailingHit)
    {
        if (string.IsNullOrEmpty(Text)) return new TextHitTestMetrics(Vector2.Zero, Vector2.Zero);

        float approxCharWidth = Size.X / Text.Length;
        float x = textPosition * approxCharWidth;
        if (isTrailingHit) x += approxCharWidth;
        x = Math.Clamp(x, 0, Size.X);

        return new TextHitTestMetrics(new Vector2(x, 0), new Vector2(approxCharWidth, Size.Y));
    }

    public TextHitTestResult HitTestPoint(Vector2 point)
    {
        if (string.IsNullOrEmpty(Text))
            return new TextHitTestResult(0, false, false, new TextHitTestMetrics(Vector2.Zero, Vector2.Zero));

        bool isInside = point.X >= 0 && point.X <= Size.X && point.Y >= 0 && point.Y <= Size.Y;
        float approxCharWidth = (Text.Length > 0) ? Size.X / Text.Length : 0;
        int textPosition = 0;
        if (approxCharWidth > 0)
        {
            textPosition = (int)(point.X / approxCharWidth);
        }
        textPosition = Math.Clamp(textPosition, 0, Text.Length);

        return new TextHitTestResult(textPosition, false, isInside,
            new TextHitTestMetrics(new Vector2(textPosition * approxCharWidth, 0), new Vector2(approxCharWidth, Size.Y)));
    }

    public void Dispose()
    {
        // Nothing to dispose
    }
}