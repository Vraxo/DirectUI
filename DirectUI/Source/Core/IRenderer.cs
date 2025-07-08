// DirectUI/Core/IRenderer.cs
using System.Numerics;
using Vortice.Direct2D1;
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
    void DrawLine(Vector2 p1, Vector2 p2, Color4 color, float strokeWidth);

    /// <summary>
    /// Draws a box with fill, border, and rounding based on a style.
    /// </summary>
    void DrawBox(Rect rect, BoxStyle style);

    /// <summary>
    /// Draws a pre-formatted text layout.
    /// </summary>
    void DrawTextLayout(Vector2 origin, ITextLayout textLayout, Color4 color);

    /// <summary>
    /// Pushes a clipping rectangle onto the stack. All subsequent drawing will be clipped to this rectangle.
    /// </summary>
    void PushClipRect(Rect rect, AntialiasMode antialiasMode = AntialiasMode.PerPrimitive);

    /// <summary>
    /// Pops the last clipping rectangle from the stack.
    /// </summary>
    void PopClipRect();

    /// <summary>
    /// Gets or creates a cached solid color brush for the given color.
    /// </summary>
    /// <returns>A solid color brush, or null if creation failed.</returns>
    ID2D1SolidColorBrush? GetOrCreateBrush(Color4 color);
}