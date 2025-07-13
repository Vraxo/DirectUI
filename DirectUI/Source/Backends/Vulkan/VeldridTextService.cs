using System.Numerics;
using DirectUI.Core;

namespace DirectUI.Backends.Vulkan;

/// <summary>
/// A Veldrid-specific implementation of the ITextService interface.
/// NOTE: This is a placeholder implementation. A real implementation would require
/// a font rasterizer (like StbTrueTypeSharp) to generate a font atlas texture.
/// </summary>
public class VeldridTextService : ITextService
{
    public VeldridTextService() { }

    public Vector2 MeasureText(string text, ButtonStyle style)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Vector2.Zero;
        }
        // Placeholder: return a rough estimate based on character count and font size.
        return new Vector2(text.Length * style.FontSize * 0.6f, style.FontSize);
    }

    public ITextLayout GetTextLayout(string text, ButtonStyle style, Vector2 maxSize, Alignment alignment)
    {
        // Placeholder: returns null. A full implementation would create a layout object
        // with metrics derived from a real font atlas.
        return null!;
    }

    public void Cleanup()
    {
        // No resources to clean up in this placeholder implementation.
    }
}