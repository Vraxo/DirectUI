using System.Numerics;
using Vortice.Mathematics;

namespace DirectUI;

public static partial class UI
{
    /// <summary>
    /// Draws an image from a byte array. The renderer caches the image based on the ID.
    /// </summary>
    /// <param name="id">A unique identifier for this image instance, used for caching.</param>
    /// <param name="imageData">The raw byte data of the image (e.g., JPEG, PNG).</param>
    /// <param name="size">The logical size to draw the image.</param>
    public static void Image(string id, byte[] imageData, Vector2 size)
    {
        if (!IsContextValid() || imageData is null || imageData.Length == 0)
        {
            // Advance layout even if there's no image data to maintain layout flow
            Context.Layout.AdvanceLayout(size);
            return;
        }

        var context = Context;
        var scale = context.UIScale;
        var logicalSize = size;
        var physicalSize = logicalSize * scale;
        var drawPos = context.Layout.ApplyLayout(Vector2.Zero);
        var bounds = new Rect(drawPos.X, drawPos.Y, physicalSize.X, physicalSize.Y);

        if (!context.Layout.IsRectVisible(bounds))
        {
            context.Layout.AdvanceLayout(logicalSize);
            return;
        }

        // The image key for the renderer's cache is derived from the widget ID.
        context.Renderer.DrawImage(imageData, id, bounds);

        context.Layout.AdvanceLayout(logicalSize);
    }
}