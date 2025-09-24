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
        // General roundness for modern look
        const float generalRoundness = 0.2f;

        // Menu Bar Buttons
        MenuButtonStyle = new ButtonStylePack { Roundness = 0, BorderLength = 0, Normal = { FillColor = Colors.Transparent }, Hover = { FillColor = DefaultTheme.HoverFill }, Pressed = { FillColor = DefaultTheme.Accent } };

        // Red "Remove" button
        RemoveButtonStyle = new ButtonStylePack
        {
            Normal = { FillColor = new Color(190, 80, 80, 255), BorderColor = new Color(120, 50, 50, 255), FontColor = Colors.WhiteSmoke },
            Hover = { FillColor = new Color(210, 90, 90, 255), FontColor = Colors.WhiteSmoke },
            Pressed = { FillColor = new Color(170, 70, 70, 255), FontColor = Colors.WhiteSmoke },
            BorderLength = 1,
            Roundness = generalRoundness
        };

        // Standard "Load" button (secondary action)
        LoadButtonStyle = new ButtonStylePack
        {
            Normal = { FillColor = DefaultTheme.NormalFill, BorderColor = DefaultTheme.NormalBorder },
            Hover = { FillColor = DefaultTheme.HoverFill, BorderColor = DefaultTheme.HoverBorder },
            Pressed = { FillColor = DefaultTheme.Accent, BorderColor = DefaultTheme.AccentBorder },
            BorderLength = 1,
            Roundness = generalRoundness
        };

        // Primary "Execute" button
        ExecuteButtonStyle = new ButtonStylePack
        {
            Normal = { FillColor = DefaultTheme.Accent, BorderColor = DefaultTheme.AccentBorder, FontColor = Colors.WhiteSmoke },
            Hover = { FillColor = DefaultTheme.AccentBorder, BorderColor = DefaultTheme.AccentBorder, FontColor = Colors.WhiteSmoke }, // Use the lighter accent for hover
            Pressed = { FillColor = new Color(0, 100, 180, 255) }, // Darker blue
            BorderLength = 1,
            Roundness = generalRoundness
        };

        // Panels
        PanelStyle = new BoxStyle { FillColor = new Color(55, 55, 55, 255), BorderLength = 1, BorderColor = new Color(30, 30, 30, 255), Roundness = 0 }; // Panels are usually not rounded
        PanelHeaderStyle = new BoxStyle { FillColor = new Color(65, 65, 65, 255), BorderLength = 0, Roundness = 0 };
        PanelHeaderTextStyle = new ButtonStyle { FontColor = new Color(220, 220, 220, 255), FontSize = 16 }; // Slightly larger header text

        // Title
        TitleTextStyle = new ButtonStyle { FontSize = 36, FontWeight = FontWeight.Light };
    }
}