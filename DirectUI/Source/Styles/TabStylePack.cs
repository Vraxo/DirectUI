using Vortice.Mathematics;

namespace DirectUI;

public sealed class TabStylePack
{
    public TabStyle Current { get; private set; }
    public TabStyle Normal { get; set; } = new();
    public TabStyle Hover { get; set; } = new();
    public TabStyle Active { get; set; } = new();
    public TabStyle ActiveHover { get; set; } = new();
    public TabStyle Disabled { get; set; } = new();

    public TabStylePack()
    {
        var panelBg = new Color4(37 / 255f, 37 / 255f, 38 / 255f, 1.0f);
        var hoverBorder = DefaultTheme.HoverBorder;
        var accentBorder = DefaultTheme.AccentBorder;

        string fontName = "Segoe UI";
        float fontSize = 14f;

        // Inactive tab
        Normal.FillColor = DefaultTheme.NormalFill;
        Normal.BorderColor = DefaultTheme.NormalBorder;
        Normal.BorderLength = 1f;
        Normal.Roundness = 0f;
        Normal.FontName = fontName; Normal.FontSize = fontSize;

        // Inactive tab, hovered
        Hover.FillColor = DefaultTheme.HoverFill;
        Hover.BorderColor = hoverBorder;
        Hover.BorderLength = 1f;
        Hover.Roundness = 0f;
        Hover.FontName = fontName; Hover.FontSize = fontSize;

        // Active tab
        Active.FillColor = panelBg;
        Active.BorderColor = hoverBorder;
        Active.BorderLengthTop = 1f; Active.BorderLengthLeft = 1f; Active.BorderLengthRight = 1f; Active.BorderLengthBottom = 0f;
        Active.Roundness = 0f;
        Active.FontName = fontName; Active.FontSize = fontSize;

        // Active tab, hovered
        ActiveHover.FillColor = panelBg;
        ActiveHover.BorderColor = accentBorder;
        ActiveHover.BorderLengthTop = 1f; ActiveHover.BorderLengthLeft = 1f; ActiveHover.BorderLengthRight = 1f; ActiveHover.BorderLengthBottom = 0f;
        ActiveHover.Roundness = 0f;
        ActiveHover.FontName = fontName; ActiveHover.FontSize = fontSize;

        Disabled.FillColor = DefaultTheme.DisabledFill;
        Disabled.BorderColor = DefaultTheme.DisabledBorder;
        Disabled.FontColor = DefaultTheme.DisabledText;
        Disabled.FontName = fontName; Disabled.FontSize = fontSize;

        Current = Normal;
    }

    public void UpdateCurrentStyle(bool isHovering, bool isActive, bool isDisabled, bool isFocused)
    {
        if (isDisabled)
        {
            Current = Disabled;
        }
        else if (isActive) Current = isHovering ? ActiveHover : Active;
        else if (isHovering) Current = Hover;
        else
        {
            Current = Normal;
        }
    }
}