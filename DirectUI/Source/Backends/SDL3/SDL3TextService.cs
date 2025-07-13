using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using DirectUI.Core;
using DirectUI.Drawing;
using SDL3;
using Vortice.DirectWrite; // For FontWeight, FontStyle, FontStretch

namespace DirectUI.Backends.SDL3;

public unsafe class SDL3TextService : ITextService
{
    private readonly Dictionary<FontKey, nint> _fontCache = new();
    private readonly Dictionary<TextLayoutCacheKey, ITextLayout> _textLayoutCache = new();
    private readonly Dictionary<(string, FontKey), Vector2> _textSizeCache = new();

    // This factor compensates for the perceptual size difference between DirectWrite and FreeType rendering.
    private const float FONT_SCALE_FACTOR = 1.125f;

    // Internal static dictionary for font file paths for SDL3
    private static readonly Dictionary<string, Dictionary<FontWeight, string>> s_sdlFontFilePaths = new();
    private static bool s_defaultFontsRegistered = false; // Flag to ensure registration happens only once

    // Internal cache key structs
    internal readonly struct FontKey(string fontName, int fontSize, FontWeight fontWeight) : IEquatable<FontKey>
    {
        private readonly string FontName = fontName;
        private readonly int FontSize = fontSize; // Stored as integer atlas size
        private readonly FontWeight FontWeight = fontWeight;
        // FontStyle and FontStretch are not directly supported by TTF.OpenFont,
        // so we omit them from the key or map them to file paths.

        public bool Equals(FontKey other) => FontName == other.FontName && FontSize.Equals(other.FontSize) && FontWeight == other.FontWeight;
        public override bool Equals(object? obj) => obj is FontKey other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(FontName, FontSize, FontWeight);
    }

    internal readonly struct TextLayoutCacheKey(string text, ButtonStyle style, Vector2 maxSize, Alignment alignment) : IEquatable<TextLayoutCacheKey>
    {
        private readonly string Text = text;
        private readonly FontKey FontKey = new(style.FontName, (int)Math.Round(style.FontSize * FONT_SCALE_FACTOR), style.FontWeight); // Cache by actual font properties
        private readonly Vector2 MaxSize = maxSize;
        private readonly HAlignment HAlign = alignment.Horizontal;
        private readonly VAlignment VAlign = alignment.Vertical;
        public bool Equals(TextLayoutCacheKey other) => Text == other.Text && MaxSize.Equals(other.MaxSize) && HAlign == other.HAlign && VAlign == other.VAlign && FontKey.Equals(other.FontKey);
        public override bool Equals(object? obj) => obj is TextLayoutCacheKey other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Text, FontKey, MaxSize, HAlign, VAlign);
    }

    public SDL3TextService()
    {
        // TTF.Init() should be called globally ONCE, not per instance.
        // It's handled by SDL3WindowHost now.
    }

    /// <summary>
    /// Registers default font paths for SDL_ttf. This should be called once globally.
    /// </summary>
    internal static void RegisterDefaultFonts()
    {
        if (s_defaultFontsRegistered) return;

        s_sdlFontFilePaths["Segoe UI"] = new Dictionary<FontWeight, string>
        {
            { FontWeight.Normal, "C:/Windows/Fonts/segoeui.ttf" },
            { FontWeight.SemiBold, "C:/Windows/Fonts/seguisb.ttf" }
        };
        s_sdlFontFilePaths["Consolas"] = new Dictionary<FontWeight, string>
        {
            { FontWeight.Normal, "C:/Windows/Fonts/consola.ttf" },
            { FontWeight.Bold, "C:/Windows/Fonts/consolab.ttf" }
        };
        s_sdlFontFilePaths["Arial"] = new Dictionary<FontWeight, string>
        {
            { FontWeight.Normal, "C:/Windows/Fonts/arial.ttf" }
        };

        s_defaultFontsRegistered = true;
    }

    /// <summary>
    /// Attempts to retrieve the file path for a registered font variant for SDL_ttf.
    /// </summary>
    internal static bool TryGetSdlFontFilePath(string familyName, FontWeight weight, out string? filePath)
    {
        filePath = null;
        if (s_sdlFontFilePaths.TryGetValue(familyName, out var variants))
        {
            if (variants.TryGetValue(weight, out filePath))
            {
                return true;
            }
            // Fallback to Normal weight if specific weight not found
            if (variants.TryGetValue(FontWeight.Normal, out filePath))
            {
                return true;
            }
        }
        // Fallback to Arial if original family not found
        if (s_sdlFontFilePaths.TryGetValue("Arial", out var arialVariants) && arialVariants.TryGetValue(FontWeight.Normal, out filePath))
        {
            Console.WriteLine($"Warning: Font family '{familyName}' (weight {weight}) not found. Falling back to Arial.");
            return true;
        }
        return false;
    }


    public nint GetOrCreateFont(string familyName, int fontSize, FontWeight weight)
    {
        FontKey key = new(familyName, fontSize, weight);

        if (_fontCache.TryGetValue(key, out nint fontPtr))
        {
            return fontPtr;
        }

        if (!TryGetSdlFontFilePath(familyName, weight, out string? filePath))
        {
            Console.WriteLine($"Warning: Could not find font file for family '{familyName}' (weight {weight}) or a fallback. Returning null font pointer.");
            return nint.Zero;
        }

        // Open font using SDL_ttf
        fontPtr = TTF.OpenFont(filePath, fontSize);

        if (fontPtr == nint.Zero)
        {
            Console.WriteLine($"Error opening font '{filePath}' at size {fontSize}: {SDL.GetError()}");
            return nint.Zero;
        }

        _fontCache[key] = fontPtr;
        return fontPtr;
    }

    public Vector2 MeasureText(string text, ButtonStyle style)
    {
        if (string.IsNullOrEmpty(text) || style is null)
            return Vector2.Zero;

        float effectiveFontSize = style.FontSize * FONT_SCALE_FACTOR;
        int atlasSize = Math.Max(1, (int)Math.Round(effectiveFontSize));

        var fontKey = new FontKey(style.FontName, atlasSize, style.FontWeight);
        var cacheKey = (text, fontKey);
        if (_textSizeCache.TryGetValue(cacheKey, out var cachedSize))
            return cachedSize;

        nint fontPtr = GetOrCreateFont(style.FontName, atlasSize, style.FontWeight);
        if (fontPtr == nint.Zero)
            return Vector2.Zero;

        if (TTF.GetStringSize(fontPtr, text, 0, out int w, out int h))
        {
            var measuredSize = new Vector2(w, h);
            _textSizeCache[cacheKey] = measuredSize;
            return measuredSize;
        }
        else
        {
            Console.WriteLine($"Error measuring text '{text}': {SDL.GetError()}");
            return Vector2.Zero;
        }
    }

    public ITextLayout GetTextLayout(string text, ButtonStyle style, Vector2 maxSize, Alignment alignment)
    {
        var layoutKey = new TextLayoutCacheKey(text, style, maxSize, alignment);
        if (_textLayoutCache.TryGetValue(layoutKey, out var cachedLayout))
        {
            return cachedLayout;
        }

        float effectiveFontSize = style.FontSize * FONT_SCALE_FACTOR;
        int atlasSize = (int)Math.Round(effectiveFontSize);
        if (atlasSize <= 0) atlasSize = 1;

        nint fontPtr = GetOrCreateFont(style.FontName, atlasSize, style.FontWeight);
        if (fontPtr == nint.Zero)
        {
            Console.WriteLine($"Error: Font not found for text layout '{text}'");
            return new SDL3TextLayout("", nint.Zero); // Return a dummy layout
        }

        var newLayout = new SDL3TextLayout(text, fontPtr);
        _textLayoutCache[layoutKey] = newLayout;
        return newLayout;
    }

    public void Cleanup()
    {
        Console.WriteLine("SDL3TextService Cleanup: Disposing cached resources...");
        // Close fonts for this specific instance's cache
        foreach (var fontPtr in _fontCache.Values)
        {
            if (fontPtr != nint.Zero)
            {
                TTF.CloseFont(fontPtr);
            }
        }
        _fontCache.Clear();
        _textLayoutCache.Clear();
        _textSizeCache.Clear();

        // TTF.Quit() is now handled globally by SDL3WindowHost
        Console.WriteLine("SDL3TextService Cleanup finished.");
    }
}