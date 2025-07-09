using DirectUI.Drawing;

namespace DirectUI;

public class TreeStyle
{
    public float Indent { get; set; } = 19f;
    public float RowHeight { get; set; } = 24f; // Increased from 22f to accommodate font descenders
    public Color LineColor { get; set; } = DefaultTheme.HoverBorder;
    public ButtonStylePack ToggleStyle { get; set; }
    public ButtonStylePack NodeLabelStyle { get; set; }

    public TreeStyle()
    {
        ToggleStyle = new ButtonStylePack
        {
            Roundness = 0.2f,
            BorderLength = 1f,
            FontName = "Consolas",
            FontSize = 14
        };
        ToggleStyle.Normal.FillColor = DefaultTheme.NormalFill;
        ToggleStyle.Normal.BorderColor = DefaultTheme.NormalBorder;
        ToggleStyle.Hover.FillColor = DefaultTheme.HoverFill;
        ToggleStyle.Hover.BorderColor = DefaultTheme.HoverBorder;
        ToggleStyle.Pressed.FillColor = DefaultTheme.Accent;
        ToggleStyle.Pressed.BorderColor = DefaultTheme.AccentBorder;

        NodeLabelStyle = new ButtonStylePack
        {
            Roundness = 0f,
            BorderLength = 0f,
            FontName = "Segoe UI",
            FontSize = 14
        };
        NodeLabelStyle.Normal.FillColor = DefaultTheme.Transparent;
        NodeLabelStyle.Normal.FontColor = DefaultTheme.Text;
        NodeLabelStyle.Hover.FillColor = new Color(64, 64, 64, 128); // Semi-transparent gray
        NodeLabelStyle.Hover.FontColor = DefaultTheme.Text;
        NodeLabelStyle.Pressed.FillColor = DefaultTheme.Accent;
        NodeLabelStyle.Pressed.FontColor = DefaultTheme.White;
        NodeLabelStyle.Pressed.BorderColor = DefaultTheme.AccentBorder;
    }
}