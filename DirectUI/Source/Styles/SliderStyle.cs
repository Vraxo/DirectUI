namespace DirectUI;

public class SliderStyle
{
    public BoxStyle Background { get; set; } = new()
    {
        FillColor = DefaultTheme.DisabledFill,
        BorderColor = DefaultTheme.NormalBorder,
        Roundness = 0.5f,
        BorderLength = 1.0f
    };

    public BoxStyle Foreground { get; set; } = new()
    {
        FillColor = DefaultTheme.Accent,
        BorderColor = DefaultTheme.Transparent,
        Roundness = 0.5f,
        BorderLength = 0.0f
    };
}
