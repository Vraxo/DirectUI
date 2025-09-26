// DirectUI/Source/Styling/YamlStyleModels.cs
using System;
using System.Collections.Generic;
using DirectUI.Animation;
using Silk.NET.OpenAL;
using Silk.NET.Vulkan;
using System.Threading.Channels;
using Vortice.Direct2D1.Effects;
using Vortice.DirectWrite;

namespace DirectUI.Styling;

internal class YamlAnimationInfo
{
    public float? Duration { get; set; }
    public string? Easing { get; set; }
}

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

    // State-specific animation
    public YamlAnimationInfo? Animation { get; set; }
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

    // Global/fallback animation properties for the pack
    public YamlAnimationInfo? Animation { get; set; }
}