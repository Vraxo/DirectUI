using DirectUI;
using Vortice.Mathematics;

namespace Daw;

public static class DawTheme
{
    // Core Palette
    public static readonly Color4 Background = new(21 / 255f, 21 / 255f, 21 / 255f, 1f);       // #151515
    public static readonly Color4 PanelBackground = new(43 / 255f, 43 / 255f, 43 / 255f, 1f);  // #2B2B2B
    public static readonly Color4 ControlFill = new(58 / 255f, 58 / 255f, 58 / 255f, 1f);      // #3A3A3A
    public static readonly Color4 ControlFillHover = new(70 / 255f, 70 / 255f, 70 / 255f, 1f); // #464646
    public static readonly Color4 Border = new(30 / 255f, 30 / 255f, 30 / 255f, 1f);           // #1E1E1E
    public static readonly Color4 Text = new(211 / 255f, 211 / 255f, 211 / 255f, 1f);          // #D3D3D3
    public static readonly Color4 TextDim = new(130 / 255f, 130 / 255f, 130 / 255f, 1f);       // #828282

    // Accent Palette (for notes, selections, etc.)
    public static readonly Color4 Accent = new(0 / 255f, 119 / 255f, 204 / 255f, 1f);          // #0077CC
    public static readonly Color4 AccentBright = new(0 / 255f, 153 / 255f, 255 / 255f, 1f);   // #0099FF
    public static readonly Color4 Selection = Colors.Yellow;

    // Piano Roll Specific
    public static readonly Color4 PianoRollGrid = new(54 / 255f, 54 / 255f, 54 / 255f, 1f);    // #363636
    public static readonly Color4 PianoRollGridAccent = new(80 / 255f, 80 / 255f, 80 / 255f, 1f); // #505050
    public static readonly Color4 PianoWhiteKey = new(218 / 255f, 218 / 255f, 218 / 255f, 1f); // #DADADA
    public static readonly Color4 PianoBlackKey = new(30 / 255f, 30 / 255f, 30 / 255f, 1f);   // #1E1E1E

    // Pre-configured Styles
    public static readonly ButtonStylePack TransportButton;
    public static readonly ButtonStylePack ToolbarButton;
    public static readonly ButtonStylePack LoopToggleStyle;

    static DawTheme()
    {
        ToolbarButton = new ButtonStylePack
        {
            Roundness = 0.2f,
            BorderLength = 1f,
        };
        ToolbarButton.Normal.FillColor = ControlFill;
        ToolbarButton.Normal.BorderColor = Border;
        ToolbarButton.Hover.FillColor = ControlFillHover;
        ToolbarButton.Hover.BorderColor = Border;
        ToolbarButton.Pressed.FillColor = AccentBright;
        ToolbarButton.Pressed.BorderColor = Accent;

        TransportButton = new ButtonStylePack
        {
            Roundness = 0.5f,
            BorderLength = 0f,
        };
        TransportButton.Normal.FillColor = Colors.Transparent;
        TransportButton.Hover.FillColor = ControlFillHover;
        TransportButton.Pressed.FillColor = Accent;

        LoopToggleStyle = new ButtonStylePack
        {
            Roundness = 0.2f,
            BorderLength = 1f,
        };
        LoopToggleStyle.Normal.FillColor = ControlFill;
        LoopToggleStyle.Normal.BorderColor = Border;
        LoopToggleStyle.Hover.FillColor = ControlFillHover;
        LoopToggleStyle.Hover.BorderColor = AccentBright;
        LoopToggleStyle.Active.FillColor = Accent;
        LoopToggleStyle.Active.BorderColor = AccentBright;
        LoopToggleStyle.ActiveHover.FillColor = AccentBright;
        LoopToggleStyle.ActiveHover.BorderColor = Accent;
    }
}
