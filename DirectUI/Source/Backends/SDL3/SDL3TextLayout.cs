using System;
using System.Numerics;
using DirectUI.Core;

namespace DirectUI.Backends.SDL3;

/// <summary>
/// A minimal implementation of ITextLayout for SDL3.
/// This will be expanded in a later step.
/// </summary>
internal class SDL3TextLayout : ITextLayout
{
    public Vector2 Size { get; }
    public string Text { get; }

    public SDL3TextLayout(string text, Vector2 size)
    {
        Text = text;
        Size = size;
    }

    public TextHitTestMetrics HitTestTextPosition(int textPosition, bool isTrailingHit)
    {
        // Placeholder
        float approxCharWidth = Size.X / Math.Max(1, Text.Length);
        return new TextHitTestMetrics(new Vector2(textPosition * approxCharWidth, 0), new Vector2(approxCharWidth, Size.Y));
    }

    public TextHitTestResult HitTestPoint(Vector2 point)
    {
        // Placeholder
        bool isInside = point.X >= 0 && point.X <= Size.X && point.Y >= 0 && point.Y <= Size.Y;
        int textPosition = (int)(point.X / (Size.X / Math.Max(1, Text.Length)));
        bool isTrailingHit = false; // Simple for now
        return new TextHitTestResult(textPosition, isTrailingHit, isInside, HitTestTextPosition(textPosition, isTrailingHit));
    }

    public void Dispose()
    {
        // No unmanaged resources currently held by this class
    }
}