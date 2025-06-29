// ButtonStyle.cs
using Vortice.Mathematics;
using Vortice.DirectWrite; // For Font related enums eventually

namespace DirectUI;

public class ButtonStyle : BoxStyle
{
    // DirectWrite Font properties will be managed elsewhere (e.g., IDWriteTextFormat)
    // public float FontSpacing { get; set; } = 0; // Handled by TextFormat
    // public float FontSize { get; set; } = 16; // Handled by TextFormat
    // public Font? Font { get; set; } = null; // Represented by TextFormat
    public Color4 FontColor { get; set; } = DefaultTheme.Text;

    // Properties needed for creating IDWriteTextFormat
    public string FontName { get; set; } = "Segoe UI";
    public float FontSize { get; set; } = 14.0f;
    public FontWeight FontWeight { get; set; } = FontWeight.Normal;
    public FontStyle FontStyle { get; set; } = FontStyle.Normal;
    public FontStretch FontStretch { get; set; } = FontStretch.Normal;

    public ButtonStyle() { }

    public ButtonStyle(ButtonStyle other) : base(other)
    {
        this.FontColor = other.FontColor;
        this.FontName = other.FontName;
        this.FontSize = other.FontSize;
        this.FontWeight = other.FontWeight;
        this.FontStyle = other.FontStyle;
        this.FontStretch = other.FontStretch;
    }
}