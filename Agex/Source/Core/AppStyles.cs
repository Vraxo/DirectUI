using DirectUI;
using DirectUI.Drawing;
using Vortice.DirectWrite;

namespace Agex;

public class AppStyles
{
    public ButtonStylePack MenuButtonStyle { get; private set; } = new();
    public ButtonStylePack RemoveButtonStyle { get; private set; } = new();
    public ButtonStylePack LoadButtonStyle { get; private set; } = new();
    public ButtonStylePack ExecuteButtonStyle { get; private set; } = new();
    public BoxStyle PanelStyle { get; private set; } = new();
    public BoxStyle PanelHeaderStyle { get; private set; } = new();
    public ButtonStyle PanelHeaderTextStyle { get; private set; } = new();
    public ButtonStyle TitleTextStyle { get; private set; } = new();

    public AppStyles()
    {
        InitializeStyles();
    }

    private void InitializeStyles()
    {
        MenuButtonStyle = new ButtonStylePack { Roundness = 0, BorderLength = 0, Normal = { FillColor = Colors.Transparent }, Hover = { FillColor = DefaultTheme.HoverFill }, Pressed = { FillColor = DefaultTheme.Accent } };
        RemoveButtonStyle = new ButtonStylePack { Normal = { FillColor = new Color(204, 63, 63, 255), BorderColor = new Color(139, 43, 43, 255) }, Hover = { FillColor = new Color(217, 76, 76, 255) }, Pressed = { FillColor = new Color(178, 55, 55, 255) }, BorderLength = 1, Roundness = 0.1f };
        LoadButtonStyle = new ButtonStylePack(new ButtonStylePack()); // Copy default
        ExecuteButtonStyle = new ButtonStylePack { Normal = { FillColor = DefaultTheme.Accent, BorderColor = DefaultTheme.AccentBorder, FontColor = Colors.WhiteSmoke }, Hover = { FillColor = new Color(77, 128, 230, 255) }, Pressed = { FillColor = new Color(40, 80, 180, 255) }, BorderLength = 1, Roundness = 0.1f };
        PanelStyle = new BoxStyle { FillColor = new Color(55, 55, 55, 255), BorderLength = 1, BorderColor = new Color(30, 30, 30, 255), Roundness = 0 };
        PanelHeaderStyle = new BoxStyle { FillColor = new Color(65, 65, 65, 255), BorderLength = 0, Roundness = 0 };
        PanelHeaderTextStyle = new ButtonStyle { FontColor = new Color(220, 220, 220, 255) };
        TitleTextStyle = new ButtonStyle { FontSize = 36, FontWeight = FontWeight.Light };
    }
}