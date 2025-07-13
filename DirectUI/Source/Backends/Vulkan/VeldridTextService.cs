using System;
using System.Numerics;
using DirectUI.Core;
using Veldrid;

namespace DirectUI.Backends.Vulkan;

/// <summary>
/// A Veldrid-specific implementation of the ITextService interface using SharpText.
/// </summary>
public class VeldridTextService : ITextService, IDisposable
{
    private bool _disposed;

    // The service now takes a GraphicsDevice to create its own text renderer for measurements.
    public VeldridTextService(GraphicsDevice gd)
    {
        // The VeldridTextRenderer is not instantiated here as it's not used by the placeholder
        // measurement logic and this service lacks the required CommandList and Font.
    }

    public Vector2 MeasureText(string text, ButtonStyle style)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Vector2.Zero;
        }

        // Placeholder implementation since SharpText.Veldrid seems to lack a public MeasureText method.
        // This provides a rough estimate for layout purposes.
        // A more accurate implementation would require access to font metrics from SharpText.
        const float characterWidthApproximationFactor = 0.6f; // Heuristic value
        float width = text.Length * style.FontSize * characterWidthApproximationFactor;
        float height = style.FontSize;
        return new Vector2(width, height);
    }

    public ITextLayout GetTextLayout(string text, ButtonStyle style, Vector2 maxSize, Alignment alignment)
    {
        // SharpText may not provide a detailed layout object for hit-testing.
        // Returning null is consistent with the previous placeholder behavior.
        return null!;
    }

    public void Cleanup()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // Nothing to dispose
            }
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Cleanup();
    }
}