using System.Collections.Generic;
using System;
using Vortice.Mathematics;
using Vortice.DirectWrite;

namespace DirectUI;

public sealed class ButtonStylePack
{
    public ButtonStyle Current { get; internal set; }
    public ButtonStyle Normal { get; set; } = new();
    public ButtonStyle Hover { get; set; } = new();
    public ButtonStyle Pressed { get; set; } = new();
    public ButtonStyle Disabled { get; set; } = new();
    public ButtonStyle Focused { get; set; } = new();
    public ButtonStyle Active { get; set; } = new();
    public ButtonStyle ActiveHover { get; set; } = new();

    public ButtonStylePack()
    {
        Hover.FillColor = DefaultTheme.HoverFill;
        Hover.BorderColor = DefaultTheme.HoverBorder;

        Pressed.FillColor = DefaultTheme.Accent;
        Pressed.BorderColor = DefaultTheme.AccentBorder;

        Disabled.FillColor = DefaultTheme.DisabledFill;
        Disabled.BorderColor = DefaultTheme.DisabledBorder;
        Disabled.FontColor = DefaultTheme.DisabledText;

        Focused.FillColor = DefaultTheme.NormalFill;
        Focused.BorderColor = DefaultTheme.FocusBorder;
        Focused.BorderLength = 1;

        // Styles for 'Active' state, taken from the old TabStylePack
        var panelBg = new Color4(37 / 255f, 37 / 255f, 38 / 255f, 1.0f);
        Active.FillColor = panelBg;
        Active.BorderColor = DefaultTheme.HoverBorder;
        Active.BorderLengthTop = 1f; Active.BorderLengthLeft = 1f; Active.BorderLengthRight = 1f; Active.BorderLengthBottom = 0f;
        Active.Roundness = 0f;

        ActiveHover.FillColor = panelBg;
        ActiveHover.BorderColor = DefaultTheme.AccentBorder;
        ActiveHover.BorderLengthTop = 1f; ActiveHover.BorderLengthLeft = 1f; ActiveHover.BorderLengthRight = 1f; ActiveHover.BorderLengthBottom = 0f;
        ActiveHover.Roundness = 0f;

        Current = Normal;
    }

    public ButtonStylePack(ButtonStylePack other)
    {
        Normal = new ButtonStyle(other.Normal);
        Hover = new ButtonStyle(other.Hover);
        Pressed = new ButtonStyle(other.Pressed);
        Disabled = new ButtonStyle(other.Disabled);
        Focused = new ButtonStyle(other.Focused);
        Active = new ButtonStyle(other.Active);
        ActiveHover = new ButtonStyle(other.ActiveHover);
        Current = Normal; // Reset current to normal
    }


    public void UpdateCurrentStyle(bool isHovering, bool isPressed, bool isDisabled, bool isFocused, bool isActive = false)
    {
        if (isDisabled)
        {
            Current = Disabled;
        }
        else if (isPressed)
        {
            Current = Pressed;
        }
        else if (isActive)
        {
            Current = isHovering ? ActiveHover : Active;
        }
        else if (isHovering)
        {
            Current = Hover;
        }
        else if (isFocused)
        {
            Current = Focused;
        }
        else
        {
            Current = Normal;
        }
    }

    private IEnumerable<ButtonStyle> AllStyles => [Normal, Hover, Pressed, Disabled, Focused, Active, ActiveHover];

    public string FontName
    {
        set => SetAll(s => s.FontName = value);
    }

    public float FontSize
    {
        set => SetAll(s => s.FontSize = value);
    }

    public FontWeight FontWeight
    {
        set => SetAll(s => s.FontWeight = value);
    }

    public FontStyle FontStyle
    {
        set => SetAll(s => s.FontStyle = value);
    }

    public FontStretch FontStretch
    {
        set => SetAll(s => s.FontStretch = value);
    }

    public Color4 FontColor
    {
        set => SetAll(s => s.FontColor = value);
    }

    public float Roundness
    {
        set => SetAll(s => s.Roundness = value);
    }

    public float BorderLength
    {
        set => SetAll(s => s.BorderLength = value);
    }

    public Color4 FillColor
    {
        set => SetAll(s => s.FillColor = value);
    }

    public Color4 BorderColor
    {
        set => SetAll(s => s.BorderColor = value);
    }

    private void SetAll(Action<ButtonStyle> setter)
    {
        foreach (ButtonStyle style in AllStyles)
        {
            setter(style);
        }
    }
}