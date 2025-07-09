// DirectUI/Backends/Raylib/RaylibRenderer.cs
using System;
using System.Collections.Generic;
using System.Numerics;
using DirectUI.Core;
using Vortice.Direct2D1; // For AntialiasMode enum, even if not used by Raylib
using Raylib_cs; // Raylib specific library

namespace DirectUI.Backends;

/// <summary>
/// A rendering backend that uses Raylib to implement the IRenderer interface.
/// </summary>
public class RaylibRenderer : IRenderer
{
    private readonly Stack<Rectangle> _clipRectStack = new();

    public Vector2 RenderTargetSize
    {
        get 
        { 
            return new(
                Raylib.GetScreenWidth(),
                Raylib.GetScreenHeight());
        }
    }

    public RaylibRenderer()
    {
        // Raylib is typically initialized externally (e.g., in Program.cs or AppHost)
        // No Raylib-specific initialization needed here in the constructor.
    }

    public void DrawLine(Vector2 p1, Vector2 p2, Drawing.Color color, float strokeWidth)
    {
        // Raylib draws lines with thickness 1 by default, or you can use DrawLineEx for thicker lines
        Raylib.DrawLineEx(p1, p2, strokeWidth, color);
    }

    public void DrawBox(Vortice.Mathematics.Rect rect, BoxStyle style)
    {
        Raylib_cs.Color fillColor = style.FillColor;
        Raylib_cs.Color borderColor = style.BorderColor;

        Raylib_cs.Rectangle rlRect = new(rect.X, rect.Y, rect.Width, rect.Height);

        // Border
        if (style.BorderColor.A > 0 && (style.BorderLengthTop > 0 || style.BorderLengthRight > 0 || style.BorderLengthBottom > 0 || style.BorderLengthLeft > 0))
        {
            if (style.Roundness > 0)
            {
                // Draw the outer rounded rectangle for the border
                Raylib.DrawRectangleRounded(rlRect, style.Roundness, 0, borderColor);

                // Calculate the inner rectangle for the fill
                var fillRect = new Raylib_cs.Rectangle(
                    rect.X + style.BorderLengthLeft,
                    rect.Y + style.BorderLengthTop,
                    rect.Width - style.BorderLengthLeft - style.BorderLengthRight,
                    rect.Height - style.BorderLengthTop - style.BorderLengthBottom
                );

                // Calculate the inner roundness
                float innerRoundness = style.Roundness; // This might need more sophisticated calculation

                // Draw the inner rounded rectangle for the fill color, effectively creating the border
                if (style.FillColor.A > 0)
                {
                    Raylib.DrawRectangleRounded(fillRect, innerRoundness, 0, fillColor);
                }
            }
            else
            {
                // Original line-by-line drawing for non-rounded rectangles
                // Top border
                if (style.BorderLengthTop > 0)
                {
                    Raylib.DrawLineEx(new Vector2(rect.X, rect.Y), new Vector2(rect.X + rect.Width, rect.Y), style.BorderLengthTop, borderColor);
                }
                // Right border
                if (style.BorderLengthRight > 0)
                {
                    Raylib.DrawLineEx(new Vector2(rect.X + rect.Width, rect.Y), new Vector2(rect.X + rect.Width, rect.Y + rect.Height), style.BorderLengthRight, borderColor);
                }
                // Bottom border
                if (style.BorderLengthBottom > 0)
                {
                    Raylib.DrawLineEx(new Vector2(rect.X + rect.Width, rect.Y + rect.Height), new Vector2(rect.X, rect.Y + rect.Height), style.BorderLengthBottom, borderColor);
                }
                // Left border
                if (style.BorderLengthLeft > 0)
                {
                    Raylib.DrawLineEx(new Vector2(rect.X, rect.Y + rect.Height), new Vector2(rect.X, rect.Y), style.BorderLengthLeft, borderColor);
                }

                // Now, draw the fill rectangle inside the borders
                if (style.FillColor.A > 0)
                {
                    var fillRect = new Raylib_cs.Rectangle(
                        rect.X + style.BorderLengthLeft,
                        rect.Y + style.BorderLengthTop,
                        rect.Width - style.BorderLengthLeft - style.BorderLengthRight,
                        rect.Height - style.BorderLengthTop - style.BorderLengthBottom
                    );
                    if (fillRect.Width > 0 && fillRect.Height > 0)
                    {
                        Raylib.DrawRectangleRec(fillRect, fillColor);
                    }
                }
            }
        }
        else if (style.FillColor.A > 0) // No border, just fill
        {
            if (style.Roundness > 0)
            {
                Raylib.DrawRectangleRounded(rlRect, style.Roundness, 0, fillColor);
            }
            else
            {
                Raylib.DrawRectangleRec(rlRect, fillColor);
            }
        }
    }

    public void DrawText(Vector2 origin, string text, ButtonStyle style, Alignment alignment, Vector2 maxSize, Drawing.Color color)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        Raylib_cs.Color rlColor = color;

        // Load font on demand (consider caching this externally or internally with FontKey)
        // For simplicity, using a default font or a very basic approach for now.
        // A robust solution would involve pre-loading fonts by style.FontName
        // and caching Raylib.Font objects.
        Font rlFont = Raylib.GetFontDefault(); // Use default font for simplicity

        // Raylib's MeasureTextEx needs a font object
        Vector2 measuredSize = Raylib.MeasureTextEx(rlFont, text, style.FontSize, style.FontSize / 10f); // Default spacing is 1/10th of font size

        Vector2 textDrawPos = origin;

        // Apply alignment based on maxSize and measuredSize
        // This logic is similar to what a text layout engine would do.
        if (maxSize.X > 0 && measuredSize.X < maxSize.X)
        {
            switch (alignment.Horizontal)
            {
                case HAlignment.Center:
                    textDrawPos.X += (maxSize.X - measuredSize.X) / 2f;
                    break;
                case HAlignment.Right:
                    textDrawPos.X += (maxSize.X - measuredSize.X);
                    break;
            }
        }
        if (maxSize.Y > 0 && measuredSize.Y < maxSize.Y)
        {
            switch (alignment.Vertical)
            {
                case VAlignment.Center:
                    textDrawPos.Y += (maxSize.Y - measuredSize.Y) / 2f;
                    break;
                case VAlignment.Bottom:
                    textDrawPos.Y += (maxSize.Y - measuredSize.Y);
                    break;
            }
        }

        // Raylib DrawTextEx takes position, text, font, font size, spacing, tint
        Raylib.DrawTextEx(rlFont, text, textDrawPos, style.FontSize, style.FontSize / 10f, rlColor);
    }

    public void PushClipRect(Vortice.Mathematics.Rect rect, AntialiasMode antialiasMode)
    {
        // Raylib has BeginScissorMode / EndScissorMode
        // Note: Raylib's scissor mode is typically integer-based.
        // Also, it's global, so nesting requires careful management.
        // We simulate a stack by saving and restoring.
        _clipRectStack.Push(new Raylib_cs.Rectangle((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height));
        Raylib.BeginScissorMode((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height);
    }

    public void PopClipRect()
    {
        if (_clipRectStack.Count <= 0)
        {
            return;
        }

        _clipRectStack.Pop();
        if (_clipRectStack.Count > 0)
        {
            // Re-apply the previous clip rect
            var prevRect = _clipRectStack.Peek();
            Raylib.BeginScissorMode((int)prevRect.X, (int)prevRect.Y, (int)prevRect.Width, (int)prevRect.Height);
        }
        else
        {
            // If stack is empty, end scissor mode entirely
            Raylib.EndScissorMode();
        }
    }

    // Raylib specific cleanup (if any resources like fonts were loaded dynamically)
    public void Cleanup()
    {
        // No Raylib-specific font objects to dispose that are loaded here per layout.
        // If fonts were loaded with LoadFontEx, they would need UnloadFont.
        _clipRectStack.Clear();
    }
}
