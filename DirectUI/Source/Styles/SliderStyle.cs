// NEW: SliderStyle.cs
// Summary: Defines the visual styles for different parts of a slider track (background/foreground).
using Vortice.Mathematics;

namespace DirectUI;

public class SliderStyle
{
    public BoxStyle Background { get; set; } = new()
    {
        FillColor = DefaultTheme.DisabledFill, // Darker background typical for sliders
        BorderColor = DefaultTheme.NormalBorder,
        Roundness = 0.5f, // Default to rounded track
        BorderThickness = 1.0f
    };

    public BoxStyle Foreground { get; set; } = new()
    {
        FillColor = DefaultTheme.Accent, // Use accent for the filled part
        BorderColor = Colors.Transparent, // Often no border on foreground
        Roundness = 0.5f, // Match background roundness
        BorderThickness = 0.0f
    };
    // Note: Grabber style is handled by ButtonStylePack in SliderDefinition
}