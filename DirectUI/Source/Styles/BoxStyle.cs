using Vortice.Mathematics;

namespace DirectUI;

public class BoxStyle
{
    public float Roundness { get; set; } = 0.2f;
    public Color4 FillColor { get; set; } = DefaultTheme.NormalFill;
    public Color4 BorderColor { get; set; } = DefaultTheme.NormalBorder;

    public float BorderLengthTop { get; set; } = 1.0f;
    public float BorderLengthRight { get; set; } = 1.0f;
    public float BorderLengthBottom { get; set; } = 1.0f;
    public float BorderLengthLeft { get; set; } = 1.0f;

    public float BorderLength
    {
        get => BorderLengthTop; // Return a representative value
        set
        {
            BorderLengthTop = value;
            BorderLengthRight = value;
            BorderLengthBottom = value;
            BorderLengthLeft = value;
        }
    }

    public BoxStyle() { }

    protected BoxStyle(BoxStyle other)
    {
        this.Roundness = other.Roundness;
        this.FillColor = other.FillColor;
        this.BorderColor = other.BorderColor;
        this.BorderLengthTop = other.BorderLengthTop;
        this.BorderLengthRight = other.BorderLengthRight;
        this.BorderLengthBottom = other.BorderLengthBottom;
        this.BorderLengthLeft = other.BorderLengthLeft;
    }
}