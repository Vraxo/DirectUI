using System;
using System.Numerics;
using DirectUI.Core;
using SDL3;
using Vortice.DirectWrite; // For FontWeight, etc.

namespace DirectUI.Backends.SDL3;

/// <summary>
/// A minimal implementation of ITextLayout for SDL3.
/// This will be expanded in a later step.
/// </summary>
internal unsafe class SDL3TextLayout : ITextLayout
{
    public Vector2 Size { get; }
    public string Text { get; }

    private readonly nint _fontPtr; // Pointer to the TTF.Font object

    public SDL3TextLayout(string text, nint fontPtr)
    {
        Text = text;
        _fontPtr = fontPtr;

        // SDL3_ttf: Use TTF.GetStringSize instead of deprecated TTF.SizeUTF8
        if (!string.IsNullOrEmpty(text) && fontPtr != nint.Zero)
        {
            if (TTF.GetStringSize(fontPtr, text, 0, out int w, out int h))
            {
                Size = new Vector2(w, h);
            }
            else
            {
                Console.WriteLine($"Error measuring text '{text}': {SDL.GetError()}");
                Size = Vector2.Zero;
            }
        }
        else
        {
            Size = Vector2.Zero;
        }
    }

    public TextHitTestMetrics HitTestTextPosition(int textPosition, bool isTrailingHit)
    {
        // Placeholder implementation as SDL_ttf does not provide direct hit-testing.
        if (string.IsNullOrEmpty(Text)) return new TextHitTestMetrics(Vector2.Zero, Vector2.Zero);

        float approxCharWidth = Size.X / Math.Max(1, Text.Length);
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
        // Placeholder implementation as SDL_ttf does not provide direct hit-testing.
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
        // This object does not own the font pointer; it's owned by SDL3TextService.
    }
}