using System;
using System.Numerics;
using DirectUI.Core;
using Veldrid;

namespace DirectUI.Backends.Vulkan;

/// <summary>
/// A Veldrid-specific implementation of the ITextService interface using SharpText.
/// NOTE: All text functionality is currently disabled for the Vulkan backend due to
/// instability in the external SharpText library.
/// </summary>
public class VeldridTextService : ITextService, IDisposable
{
    private bool _disposed;

    public VeldridTextService(GraphicsDevice gd)
    {
        // No-op.
    }

    public Vector2 MeasureText(string text, ButtonStyle style)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Vector2.Zero;
        }

        // Restore placeholder measurement to allow layout to work correctly.
        // This provides a rough estimate for layout purposes even though text isn't rendered.
        const float characterWidthApproximationFactor = 0.6f; // Heuristic value
        float width = text.Length * style.FontSize * characterWidthApproximationFactor;
        float height = style.FontSize;
        return new Vector2(width, height);
    }

    public ITextLayout GetTextLayout(string text, ButtonStyle style, Vector2 maxSize, Alignment alignment)
    {
        // Return null as text rendering is disabled and no layout object is available.
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