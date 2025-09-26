using System;
using System.Collections.Generic;
using System.Numerics;
using DirectUI.Drawing;

namespace DirectUI;

public static partial class UI
{
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

        // This theme is created once per call and configured to use the Button's `isActive` state.
        var swatchTheme = new ButtonStylePack
        {
            Roundness = 0.5f,
            BorderLength = 3f,
        };
        // The 'Active' state (when isActive is true) has a white border.
        swatchTheme.Active.BorderColor = Colors.White;
        swatchTheme.ActiveHover.BorderColor = Colors.White; // Keep border on active hover

        // The 'Normal' state has a transparent border.
        swatchTheme.Normal.BorderColor = Colors.Transparent;
        swatchTheme.Hover.BorderColor = Colors.Transparent; // No border change on hover

        BeginHBoxContainer(id + "_hbox", Context.Layout.GetCurrentPosition(), gap);

        foreach (var colorHex in availableColors)
        {
            bool isSelected = colorHex == selectedColorHex;

            // Since the fill color is different for each button, we must modify the theme
            // inside the loop before passing it to the Button call.
            var fillColor = ParseColorHex(colorHex);
            swatchTheme.Normal.FillColor = fillColor;
            swatchTheme.Hover.FillColor = fillColor;
            swatchTheme.Active.FillColor = fillColor;
            swatchTheme.ActiveHover.FillColor = fillColor;

            // Use the `isActive` parameter to control the selection state visually.
            if (Button(id + "_swatch_" + colorHex, "", size: swatchSize, theme: swatchTheme, isActive: isSelected))
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