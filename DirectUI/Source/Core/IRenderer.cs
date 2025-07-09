// DirectUI/Core/IRenderer.cs
using System.Numerics;
using DirectUI.Drawing;
using Vortice.Direct2D1; // Required for AntialiasMode
using Vortice.Mathematics;

namespace DirectUI.Core;

/// <summary>
/// Defines the abstract contract for a rendering backend.
/// The UI library uses this interface to issue all drawing commands.
/// </summary>
public interface IRenderer
{
    /// <summary>
    /// Gets the current size of the render target.
    /// </summary>
    Vector2 RenderTargetSize { get; }

    /// <summary>
    /// Draws a line between two points.
    /// </summary>
    void DrawLine(Vector2 p1, Vector2 p2, Color color, float strokeWidth);

    /// <summary>
    /// Draws a box with fill, border, and rounding based on a style.
    /// </summary>
    void DrawBox(Vortice.Mathematics.Rect rect, BoxStyle style);

    /// <summary>
    /// Draws text. The renderer is responsible for its own text layout and caching.
    /// </summary>
    void DrawText(Vector2 origin, string text, ButtonStyle style, Alignment alignment, Vector2 maxSize, Color color);

    /// <summary>
    /// Pushes a clipping rectangle onto the stack. All subsequent drawing will be clipped to this rectangle.
    /// </summary>
    void PushClipRect(Vortice.Mathematics.Rect rect, AntialiasMode antialiasMode = AntialiasMode.PerPrimitive);

    /// <summary>
    /// Pops the last clipping rectangle from the stack.
    /// </summary>
    void PopClipRect();
}
