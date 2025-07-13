using System.Numerics;
using DirectUI.Core;
using SDL3;
using Veldrid.Sdl2;
using Vortice.Direct2D1; // For AntialiasMode enum, even if not used by SDL
using Vortice.Mathematics;

namespace DirectUI.Backends.SDL3;

public unsafe class SDL3Renderer : IRenderer
{
    private readonly nint _rendererPtr;
    private readonly nint _windowPtr;

    private int _windowWidth;
    private int _windowHeight;

    private readonly Stack<Rect> _clipRectStack = new();

    public Vector2 RenderTargetSize
    {
        get
        {
            // SDL_GetWindowSizeInPixels should be used if DPI scaling is a concern.
            SDL.GetWindowSize(_windowPtr, out _windowWidth, out _windowHeight);
            return new(_windowWidth, _windowHeight);
        }
    }

    public SDL3Renderer(nint rendererPtr, nint windowPtr)
    {
        _rendererPtr = rendererPtr;
        _windowPtr = windowPtr;

        SDL.GetWindowSize(_windowPtr, out _windowWidth, out _windowHeight);
    }

    internal void UpdateWindowSize(int width, int height)
    {
        _windowWidth = width;
        _windowHeight = height;
    }

    public void DrawLine(Vector2 p1, Vector2 p2, Drawing.Color color, float strokeWidth)
    {
        SDL.SetRenderDrawColor(_rendererPtr, color.R, color.G, color.B, color.A);

        if (strokeWidth <= 1.0f)
        {
            // Draw 1-pixel thin line
            SDL.RenderLine(_rendererPtr, (int)p1.X, (int)p1.Y, (int)p2.X, (int)p2.Y);
        }
        else
        {
            // For thicker lines, approximate with a filled rectangle.
            // This is a basic approximation, primarily for axis-aligned or near-axis-aligned lines.
            // For general diagonal lines with thickness, more complex geometry (like a rotated rectangle or two triangles)
            // or a dedicated library (e.g., SDL_gfx or custom shader) would be required.

            float halfStroke = strokeWidth / 2f;
            float dx = p2.X - p1.X;
            float dy = p2.Y - p1.Y;

            // If it's more horizontal
            if (Math.Abs(dx) >= Math.Abs(dy))
            {
                SDL.FRect rectangle = new()
                {
                    X = Math.Min(p1.X, p2.X),
                    Y = (p1.Y + p2.Y) / 2f - halfStroke, // Center Y on the line's average Y
                    W = Math.Abs(dx),
                    H = strokeWidth
                };
                SDL.RenderFillRect(_rendererPtr, rectangle);
            }
            // If it's more vertical
            else
            {
                SDL.FRect rectangle = new()
                {
                    X = (p1.X + p2.X) / 2f - halfStroke, // Center X on the line's average X
                    Y = Math.Min(p1.Y, p2.Y),
                    W = strokeWidth,
                    H = Math.Abs(dy)
                };
                SDL.RenderFillRect(_rendererPtr, rectangle);
            }
        }
    }

    public void DrawBox(Vortice.Mathematics.Rect rect, BoxStyle style)
    {
        // Convert Vortice.Mathematics.Rect to SDL.FRect for drawing
        SDL.FRect outerRect = new()
        {
            X = rect.X,
            Y = rect.Y,
            W = rect.Width,
            H = rect.Height
        };

        // 1. Draw the border background (entire rectangle with border color)
        // If border color is transparent or all border lengths are zero, this step effectively does nothing visually.
        if (style.BorderColor.A > 0 && (style.BorderLengthTop > 0 || style.BorderLengthRight > 0 || style.BorderLengthBottom > 0 || style.BorderLengthLeft > 0))
        {
            SDL.SetRenderDrawColor(_rendererPtr, style.BorderColor.R, style.BorderColor.G, style.BorderColor.B, style.BorderColor.A);
            SDL.RenderFillRect(_rendererPtr, outerRect);
        }

        // 2. Draw the inner fill rectangle (inset by border lengths)
        if (style.FillColor.A > 0)
        {
            float fillX = rect.X + style.BorderLengthLeft;
            float fillY = rect.Y + style.BorderLengthTop;
            float fillWidth = Math.Max(0f, rect.Width - style.BorderLengthLeft - style.BorderLengthRight);
            float fillHeight = Math.Max(0f, rect.Height - style.BorderLengthTop - style.BorderLengthBottom);

            if (fillWidth > 0 && fillHeight > 0)
            {
                SDL.FRect fillRect = new()
                {
                    X = fillX,
                    Y = fillY,
                    W = fillWidth,
                    H = fillHeight
                };

                SDL.SetRenderDrawColor(_rendererPtr, style.FillColor.R, style.FillColor.G, style.FillColor.B, style.FillColor.A);
                SDL.RenderFillRect(_rendererPtr, fillRect);
            }
            // Special case: If no border is defined (all lengths 0) and the fill itself is opaque,
            // the above fillRect might be invalid (e.g. if original rect had 0 width/height)
            // In such a scenario, we still want to draw the full outerRect with the fill color if it's visible.
            else if (style.BorderLengthTop == 0 && style.BorderLengthRight == 0 && style.BorderLengthBottom == 0 && style.BorderLengthLeft == 0 && (outerRect.W > 0 && outerRect.H > 0))
            {
                SDL.SetRenderDrawColor(_rendererPtr, style.FillColor.R, style.FillColor.G, style.FillColor.B, style.FillColor.A);
                SDL.RenderFillRect(_rendererPtr, outerRect);
            }
        }
        // Roundness is not natively supported by SDL's basic renderer. It will be drawn as square corners.
    }

    public void DrawText(Vector2 origin, string text, ButtonStyle style, Alignment alignment, Vector2 maxSize, Drawing.Color color)
    {
        // Not implemented in step 1. Will draw nothing.
        // For full text rendering, SDL_ttf would be needed, and it's a multi-step process
        // (load font, render text to surface, create texture from surface, copy texture).
        // This will be implemented in a later step.
    }

    public void PushClipRect(Rect rect, AntialiasMode antialiasMode)
    {
        _clipRectStack.Push(rect);
        ApplyClipRect(rect);
    }

    public void PopClipRect()
    {
        if (_clipRectStack.Count > 0)
        {
            _clipRectStack.Pop();
        }

        if (_clipRectStack.Count > 0)
        {
            ApplyClipRect(_clipRectStack.Peek());
        }
        else
        {
            SDL.Rect rect = new()
            {
                X = 0,
                Y = 0,
                W = 0,
                H = 0,
            };

            SDL.SetRenderClipRect(_rendererPtr, rect);
        }
    }

    private void ApplyClipRect(Rect rect)
    {
        SDL.Rect clipRect = new()
        {
            X = (int)float.Floor(rect.X),
            Y = (int)float.Floor(rect.Y),
            W = (int)float.Ceiling(rect.Width + (rect.X - float.Floor(rect.X))),
            H = (int)float.Ceiling(rect.Height + (rect.Y - float.Floor(rect.Y)))
        };

        SDL.SetRenderClipRect(_rendererPtr, clipRect);
    }

    public void Flush()
    {
        // SDL's 2D renderer typically renders commands immediately to an internal buffer.
        // The SDL_RenderPresent call (handled by SDL3UIHost) does the actual flush to screen.
    }

    public void Cleanup()
    {
        _clipRectStack.Clear();
        // The renderer and window are managed and destroyed by ApplicationRunner.
    }
}