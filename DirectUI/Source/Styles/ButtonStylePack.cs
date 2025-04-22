// ButtonStylePack.cs
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
        // Apply default theme colors/styles
        Hover.FillColor = DefaultTheme.HoverFill;
        Hover.BorderColor = DefaultTheme.HoverBorder; // Added default

        Pressed.FillColor = DefaultTheme.Accent;
        Pressed.BorderColor = DefaultTheme.AccentBorder; // Added default

        Disabled.FillColor = DefaultTheme.DisabledFill;
        Disabled.BorderColor = DefaultTheme.DisabledBorder;
        Disabled.FontColor = DefaultTheme.DisabledText;

        // Focused state defaults (if/when implemented)
        // Focused.BorderColor = DefaultTheme.FocusBorder;
        // Focused.BorderThickness = 2.0f; // Example

        Current = Normal; // Start with Normal style
    }

    // Method to update the Current style based on button state
    // This will be called by the UI logic
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
        // else if (isFocused && !isHovering) // Focus state TBD
        // {
        //     Current = Focused;
        // }
        else if (isHovering)
        {
            Current = Hover;
        }
        else
        {
            Current = Normal;
        }
    }

    // --- Convenience Setters (Optional but can be useful) ---

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

    public float BorderThickness
    {
        set => SetAll(s => s.BorderThickness = value);
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
            // Also update Current if it happens to be one of the base styles
            // Note: This direct application might be overridden by UpdateCurrentStyle later
            setter(style);
        }
        // Re-apply to Current just in case it was a distinct instance,
        // though UpdateCurrentStyle is the main way Current is set.
        setter(Current);
    }
}