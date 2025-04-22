// BoxStyle.cs
using Vortice.Mathematics;

namespace DirectUI;

public class BoxStyle
{
    public float Roundness { get; set; } = 0.2f; // Relative roundness (0 = sharp, 1 = fully rounded)
    public Color4 FillColor { get; set; } = DefaultTheme.NormalFill;
    public Color4 BorderColor { get; set; } = DefaultTheme.NormalBorder;
    public float BorderThickness { get; set; } = 1.0f; // Uniform border thickness
}