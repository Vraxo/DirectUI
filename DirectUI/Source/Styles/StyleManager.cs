// DirectUI/Source/Styling/StyleManager.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using DirectUI.Drawing;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace DirectUI.Styling;

public static class StyleManager
{
    private static readonly Dictionary<string, object> s_styleCache = new();
    private static readonly IDeserializer s_yamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(PascalCaseNamingConvention.Instance)
        .Build();

    public static void LoadStylesFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"[StyleManager] Warning: Style file not found at '{filePath}'.");
            return;
        }

        try
        {
            string yamlContent = File.ReadAllText(filePath);
            var yamlStyles = s_yamlDeserializer.Deserialize<Dictionary<string, YamlButtonStylePack>>(yamlContent);

            foreach (var kvp in yamlStyles)
            {
                var stylePack = ConvertFromYaml(kvp.Value);
                s_styleCache[kvp.Key] = stylePack;
            }

            Console.WriteLine($"[StyleManager] Successfully loaded {yamlStyles.Count} styles from '{filePath}'.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[StyleManager] Error loading styles from '{filePath}': {ex.Message}");
        }
    }

    public static T Get<T>(string key) where T : class, new()
    {
        if (s_styleCache.TryGetValue(key, out var style))
        {
            if (style is T typedStyle)
            {
                // Return a copy to prevent modification of the cached master style.
                if (typedStyle is ButtonStylePack bsp)
                {
                    return new ButtonStylePack(bsp) as T ?? new T();
                }
                // Add other copy-constructible style types here in the future
                return typedStyle;
            }
        }
        Console.WriteLine($"[StyleManager] Warning: Style key '{key}' not found. Returning new default style.");
        return new T();
    }

    private static ButtonStylePack ConvertFromYaml(YamlButtonStylePack yamlPack)
    {
        var pack = new ButtonStylePack();

        // Apply pack-level properties to all styles first
        if (yamlPack.Roundness.HasValue) pack.Roundness = yamlPack.Roundness.Value;
        if (yamlPack.BorderLength.HasValue) pack.BorderLength = yamlPack.BorderLength.Value;
        if (yamlPack.FontName != null) pack.FontName = yamlPack.FontName;
        if (yamlPack.FontSize.HasValue) pack.FontSize = yamlPack.FontSize.Value;
        if (yamlPack.FontWeight.HasValue) pack.FontWeight = yamlPack.FontWeight.Value;
        if (yamlPack.FontStyle.HasValue) pack.FontStyle = yamlPack.FontStyle.Value;
        if (yamlPack.FontStretch.HasValue) pack.FontStretch = yamlPack.FontStretch.Value;

        // Apply specific style overrides
        ApplyYamlStyle(pack.Normal, yamlPack.Normal);
        ApplyYamlStyle(pack.Hover, yamlPack.Hover);
        ApplyYamlStyle(pack.Pressed, yamlPack.Pressed);
        ApplyYamlStyle(pack.Disabled, yamlPack.Disabled);
        ApplyYamlStyle(pack.Focused, yamlPack.Focused);
        ApplyYamlStyle(pack.Active, yamlPack.Active);
        ApplyYamlStyle(pack.ActiveHover, yamlPack.ActiveHover);

        return pack;
    }

    private static void ApplyYamlStyle(ButtonStyle target, YamlButtonStyle? source)
    {
        if (source == null) return;

        // BoxStyle properties
        if (source.Roundness.HasValue) target.Roundness = source.Roundness.Value;
        if (source.FillColor != null) target.FillColor = ParseColor(source.FillColor);
        if (source.BorderColor != null) target.BorderColor = ParseColor(source.BorderColor);
        if (source.BorderLength.HasValue) target.BorderLength = source.BorderLength.Value;
        if (source.BorderLengthTop.HasValue) target.BorderLengthTop = source.BorderLengthTop.Value;
        if (source.BorderLengthRight.HasValue) target.BorderLengthRight = source.BorderLengthRight.Value;
        if (source.BorderLengthBottom.HasValue) target.BorderLengthBottom = source.BorderLengthBottom.Value;
        if (source.BorderLengthLeft.HasValue) target.BorderLengthLeft = source.BorderLengthLeft.Value;

        // ButtonStyle properties
        if (source.FontColor != null) target.FontColor = ParseColor(source.FontColor);
        if (source.FontName != null) target.FontName = source.FontName;
        if (source.FontSize.HasValue) target.FontSize = source.FontSize.Value;
        if (source.FontWeight.HasValue) target.FontWeight = source.FontWeight.Value;
        if (source.FontStyle.HasValue) target.FontStyle = source.FontStyle.Value;
        if (source.FontStretch.HasValue) target.FontStretch = source.FontStretch.Value;
        if (source.Scale != null && source.Scale.Count == 2) target.Scale = new Vector2(source.Scale[0], source.Scale[1]);
    }

    private static Color ParseColor(string colorStr)
    {
        if (string.IsNullOrEmpty(colorStr) || !colorStr.StartsWith("#"))
        {
            Console.WriteLine($"[StyleManager] Warning: Invalid color format '{colorStr}'. Defaulting to transparent.");
            return Colors.Transparent;
        }

        try
        {
            // #RRGGBB or #RRGGBBAA
            if (colorStr.Length != 7 && colorStr.Length != 9)
                throw new FormatException("Hex color must be in #RRGGBB or #RRGGBBAA format.");

            byte r = Convert.ToByte(colorStr.Substring(1, 2), 16);
            byte g = Convert.ToByte(colorStr.Substring(3, 2), 16);
            byte b = Convert.ToByte(colorStr.Substring(5, 2), 16);
            byte a = 255;
            if (colorStr.Length == 9)
            {
                a = Convert.ToByte(colorStr.Substring(7, 2), 16);
            }
            return new Color(r, g, b, a);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[StyleManager] Warning: Could not parse color '{colorStr}'. Error: {ex.Message}. Defaulting to transparent.");
            return Colors.Transparent;
        }
    }
}