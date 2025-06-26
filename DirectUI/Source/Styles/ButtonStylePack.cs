// MODIFIED: Styles/ButtonStylePack.cs
// Summary: Updated BorderThickness convenience setter to use the new BorderLength setter in BoxStyle. Added Obsolete attribute to BorderThickness.
using System.Collections.Generic;
using System;
using Vortice.Mathematics;
using Vortice.DirectWrite;

namespace DirectUI;

public sealed class ButtonStylePack
{
    public ButtonStyle Current { get; private set; }
    public ButtonStyle Normal { get; set; } = new();
    public ButtonStyle Hover { get; set; } = new();
    public ButtonStyle Pressed { get; set; } = new();
    public ButtonStyle Disabled { get; set; } = new();
    // public ButtonStyle Focused { get; set; } = new(); // Focus state not implemented yet

    public ButtonStylePack()
    {
        Hover.FillColor = DefaultTheme.HoverFill;
        Hover.BorderColor = DefaultTheme.HoverBorder;

        Pressed.FillColor = DefaultTheme.Accent;
        Pressed.BorderColor = DefaultTheme.AccentBorder;

        Disabled.FillColor = DefaultTheme.DisabledFill;
        Disabled.BorderColor = DefaultTheme.DisabledBorder;
        Disabled.FontColor = DefaultTheme.DisabledText;

        Current = Normal;
    }

    public void UpdateCurrentStyle(bool isHovering, bool isPressed, bool isDisabled /*, bool isFocused */)
    {
        if (isDisabled)
        {
            Current = Disabled;
        }
        else if (isPressed)
        {
            Current = Pressed;
        }
        else if (isHovering)
        {
            Current = Hover;
        }
        //else if (isFocused) // Future focus state
        //{
        //    Current = Focused;
        //}
        else
        {
            Current = Normal;
        }
    }

    private IEnumerable<ButtonStyle> AllStyles => [Normal, Hover, Pressed, Disabled /*, Focused*/];

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

    // Updated setter
    public float BorderLength
    {
        set => SetAll(s => s.BorderLength = value);
    }

    // Obsolete - kept for backward compatibility or remove if breaking change is ok
    [Obsolete("Use BorderLength instead.")]
    public float BorderThickness
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
        // Update Current directly after modifying the source styles
        // No need to call setter(Current) separately if UpdateCurrentStyle is called later.
        // If UpdateCurrentStyle might not be called, uncommenting setter(Current) ensures immediate consistency.
        // setter(Current);
    }
}