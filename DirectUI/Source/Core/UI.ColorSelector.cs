using System;
using System.Collections.Generic;
using System.Numerics;
using DirectUI.Drawing;

namespace DirectUI;

public static partial class UI
{
    /// <summary>
    /// Displays a horizontal row of color swatches for selection.
    /// </summary>
    /// <param name="id">A unique identifier for the control.</param>
    /// <param name="selectedColorHex">A reference to the string holding the selected color in "#RRGGBB" format.</param>
    /// <param name="availableColors">A list of available colors in "#RRGGBB" format.</param>
    /// <param name="swatchSize">The size of each color swatch button.</param>
    /// <param name="gap">The horizontal gap between swatches.</param>
    /// <returns>True if the selection changed this frame, otherwise false.</returns>
    public static bool ColorSelector(
        string id,
        ref string selectedColorHex,
        IReadOnlyList<string> availableColors,
        Vector2 swatchSize,
        float gap = 10f)
    {
        if (!IsContextValid() || availableColors is null || availableColors.Count == 0)
        {
            return false;
        }

        bool selectionChanged = false;

        BeginHBoxContainer(id + "_hbox", Context.Layout.GetCurrentPosition(), gap);

        foreach (var colorHex in availableColors)
        {
            bool isSelected = colorHex == selectedColorHex;

            // The theme is dependent on the isSelected state, so we construct it every frame.
            // This is a common and acceptable pattern in immediate mode GUI.
            var swatchTheme = new ButtonStylePack
            {
                Roundness = 0.5f,
                Normal =
                {
                    FillColor = ParseColorHex(colorHex),
                    BorderColor = isSelected ? Colors.White : Colors.Transparent,
                    BorderLength = 3f
                }
            };
            // Ensure hover state doesn't look out of place
            swatchTheme.Hover.FillColor = swatchTheme.Normal.FillColor;
            swatchTheme.Hover.BorderColor = swatchTheme.Normal.BorderColor;
            swatchTheme.Hover.BorderLength = swatchTheme.Normal.BorderLength;


            if (Button(id + "_swatch_" + colorHex, "", size: swatchSize, theme: swatchTheme))
            {
                if (!isSelected)
                {
                    selectedColorHex = colorHex;
                    selectionChanged = true;
                }
            }
        }

        EndHBoxContainer();

        return selectionChanged;
    }

    /// <summary>
    /// A private helper to parse a hex color string, with a fallback.
    /// </summary>
    private static Color ParseColorHex(string hex)
    {
        if (!string.IsNullOrEmpty(hex) && hex.StartsWith("#") && hex.Length == 7)
        {
            try
            {
                byte r = Convert.ToByte(hex.Substring(1, 2), 16);
                byte g = Convert.ToByte(hex.Substring(3, 2), 16);
                byte b = Convert.ToByte(hex.Substring(5, 2), 16);
                return new Color(r, g, b, 255);
            }
            catch
            {
                // Fallback on parsing error
                return DefaultTheme.Accent;
            }
        }
        // Fallback if format is wrong
        return DefaultTheme.Accent;
    }
}