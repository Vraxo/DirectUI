// DirectUI/Backends/Raylib/RaylibRenderer.cs
using System;
using System.Collections.Generic;
using System.Numerics;
using DirectUI.Core;
using DirectUI.Drawing;
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

        // Font Rendering Strategy:
        // No oversampling. The font is loaded into an atlas at the nearest integer size to the requested style.FontSize.
        // This avoids scaling artifacts from oversampling that can cause "shaggy" or blurry text.
        // Point filtering is used in FontManager to ensure sharp pixels from the atlas.
        // MSAA (enabled globally) is relied upon for anti-aliasing the final glyph geometry.
        int atlasSize = (int)Math.Round(style.FontSize);
        if (atlasSize <= 0) atlasSize = 1; // Prevent loading font at size 0.

        Raylib_cs.Color rlColor = color;

        // Use the FontManager to get the appropriate font, loaded at the native resolution.
        Font rlFont = FontManager.GetFont(style.FontName, atlasSize);

        // Measure and Draw using the original float font size.
        // Raylib will scale the glyphs from the atlas (rasterized at 'atlasSize') to match 'style.FontSize'.
        // Since these two values are very close, scaling artifacts are minimized.
        Vector2 measuredSize = Raylib.MeasureTextEx(rlFont, text, style.FontSize, style.FontSize / 10f);

        Vector2 textDrawPos = origin;

        // Apply horizontal alignment
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

        // Apply vertical alignment
        if (maxSize.Y > 0)
        {
            switch (alignment.Vertical)
            {
                case VAlignment.Center:
                    // FIX: Use the measured height for centering. Using the font's point size (style.FontSize)
                    // does not account for descenders and can cause the bottom of the text to be clipped
                    // in tight containers. The measured height provides the actual bounding box of the rendered text.
                    textDrawPos.Y += (maxSize.Y - measuredSize.Y) / 2f;
                    break;
                case VAlignment.Bottom:
                    textDrawPos.Y += (maxSize.Y - measuredSize.Y);
                    break;
            }
        }

        // Round the final position to the nearest whole pixel to prevent sub-pixel "wobble".
        textDrawPos = new Vector2(MathF.Round(textDrawPos.X), MathF.Round(textDrawPos.Y));

        // Draw using the original float font size.
        Raylib.DrawTextEx(rlFont, text, textDrawPos, style.FontSize, style.FontSize / 10f, rlColor);
    }

    public void PushClipRect(Vortice.Mathematics.Rect rect, AntialiasMode antialiasMode)
    {
        // Store the original float rect on our stack to preserve precision.
        _clipRectStack.Push(new Raylib_cs.Rectangle(rect.X, rect.Y, rect.Width, rect.Height));
        ApplyScissorFromRect(rect);
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
            // Re-apply the previous clip rect from the stack.
            var prevRect = _clipRectStack.Peek();
            ApplyScissorFromRect(new Vortice.Mathematics.Rect(prevRect.X, prevRect.Y, prevRect.Width, prevRect.Height));
        }
        else
        {
            // If stack is empty, end scissor mode entirely.
            Raylib.EndScissorMode();
        }
    }

    private void ApplyScissorFromRect(Vortice.Mathematics.Rect rect)
    {
        // Raylib's scissor mode is integer-based. To avoid clipping artifacts with
        // fractional coordinates, we calculate an integer-based bounding box that
        // fully contains the desired float-based rectangle.
        int x = (int)MathF.Floor(rect.X);
        int y = (int)MathF.Floor(rect.Y);
        // Add the fractional part of the origin back to the size before ceiling to ensure the rect is fully covered.
        int width = (int)MathF.Ceiling(rect.Width + (rect.X - x));
        int height = (int)MathF.Ceiling(rect.Height + (rect.Y - y));

        Raylib.BeginScissorMode(x, y, width, height);
    }

    // Raylib specific cleanup (if any resources like fonts were loaded dynamically)
    public void Cleanup()
    {
        // No Raylib-specific font objects to dispose that are loaded here per layout.
        // If fonts were loaded with LoadFontEx, they would need UnloadFont.
        _clipRectStack.Clear();
    }
}