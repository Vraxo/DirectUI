using System.Numerics;
using DirectUI.Core;

namespace DirectUI.Backends.Vulkan;

/// <summary>
/// A Veldrid-specific implementation of the ITextService interface.
/// This implementation uses a font manager to get font atlases for measurement.
/// </summary>
public class VeldridTextService : ITextService
{
    private readonly VulkanFontManager _fontManager;

    public VeldridTextService(VulkanFontManager fontManager)
    {
        _fontManager = fontManager;
    }

    public Vector2 MeasureText(string text, ButtonStyle style)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Vector2.Zero;
        }

        var fontAtlas = _fontManager.GetAtlas(style.FontName, style.FontSize);
        return fontAtlas.MeasureText(text);
    }

    public ITextLayout GetTextLayout(string text, ButtonStyle style, Vector2 maxSize, Alignment alignment)
    {
        // This is a simplified implementation that doesn't handle wrapping or advanced layout.
        // It returns a layout object with the measured size of the entire string.
        var size = MeasureText(text, style);
        return new VeldridTextLayout(text, size);
    }

    public void Cleanup()
    {
        // FontManager is owned by VeldridUIHost, so we don't dispose it here.
    }
}