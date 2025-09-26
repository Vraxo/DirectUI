// DirectUI/Source/Styling/YamlStyleModels.cs
using System.Collections.Generic;
using Vortice.DirectWrite;

namespace DirectUI.Styling;

// These models are used specifically for deserializing from YAML.
// They use simple types (like string for color) that are easy to write in YAML.
// They are then converted to the real style objects used by the UI.

internal class YamlButtonStyle
{
    // BoxStyle properties
    public float? Roundness { get; set; }
    public object? FillColor { get; set; }
    public object? BorderColor { get; set; }
    public float? BorderLength { get; set; }
    public float? BorderLengthTop { get; set; }
    public float? BorderLengthRight { get; set; }
    public float? BorderLengthBottom { get; set; }
    public float? BorderLengthLeft { get; set; }

    // ButtonStyle properties
    public object? FontColor { get; set; }
    public string? FontName { get; set; }
    public float? FontSize { get; set; }
    public FontWeight? FontWeight { get; set; }
    public FontStyle? FontStyle { get; set; }
    public FontStretch? FontStretch { get; set; }
    public List<float>? Scale { get; set; }
}

internal class YamlButtonStylePack
{
    // Properties to match ButtonStylePack states
    public YamlButtonStyle? Normal { get; set; }
    public YamlButtonStyle? Hover { get; set; }
    public YamlButtonStyle? Pressed { get; set; }
    public YamlButtonStyle? Disabled { get; set; }
    public YamlButtonStyle? Focused { get; set; }
    public YamlButtonStyle? Active { get; set; }
    public YamlButtonStyle? ActiveHover { get; set; }

    // Properties that can be set on the pack level
    public float? Roundness { get; set; }
    public float? BorderLength { get; set; }
    public string? FontName { get; set; }
    public float? FontSize { get; set; }
    public FontWeight? FontWeight { get; set; }
    public FontStyle? FontStyle { get; set; }
    public FontStretch? FontStretch { get; set; }
}