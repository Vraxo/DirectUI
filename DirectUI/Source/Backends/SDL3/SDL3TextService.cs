using System;
using System.Collections.Generic;
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

    // Internal cache key structs
    internal readonly struct FontKey(string fontName, int fontSize, FontWeight fontWeight) : IEquatable<FontKey>
    {
        private readonly string FontName = fontName;
        private readonly int FontSize = fontSize; // Stored as integer atlas size
        private readonly FontWeight FontWeight = fontWeight;
        // FontStyle and FontStretch are not directly supported by TTF.OpenFont,
        // so we omit them from the key or map them to file paths.
        // For simplicity, we're not using them for font selection for now.
        // private readonly FontStyle FontStyle = style.FontStyle;
        // private readonly FontStretch FontStretch = style.FontStretch;

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
        TTF.Init();
        // Register font variants for SDL3
        // This needs to be consistent with the font paths used by Direct2D/Raylib AppHost
        // For SDL_ttf, typically only Normal/Bold weights are distinct font files.
        // If a specific weight isn't found, TTF will try to simulate it, but it's better to provide the file.
        // Assuming "Segoe UI" and "Consolas" are available at these paths or system default.
        FontManager.Initialize(); // Initialize the common FontManager if not already
        FontManager.RegisterFontVariant("Segoe UI", FontWeight.Normal, "C:/Windows/Fonts/segoeui.ttf");
        FontManager.RegisterFontVariant("Segoe UI", FontWeight.SemiBold, "C:/Windows/Fonts/seguisb.ttf");
        FontManager.RegisterFontVariant("Consolas", FontWeight.Normal, "C:/Windows/Fonts/consola.ttf");
        FontManager.RegisterFontVariant("Consolas", FontWeight.Bold, "C:/Windows/Fonts/consolab.ttf");
    }

    public nint GetOrCreateFont(string familyName, int fontSize, FontWeight weight)
    {
        var key = new FontKey(familyName, fontSize, weight);
        if (_fontCache.TryGetValue(key, out nint fontPtr))
        {
            return fontPtr;
        }

        // In a real application, you'd map familyName and weight to actual font file paths.
        // For simplicity, we'll use a hardcoded fallback or look up via FontManager.
        // FontManager.GetFont is for Raylib specifically, so we'll do the file path lookup here.
        string? filePath = null;

        // Try to get the specific weight first, then fallback to normal
        if (!Drawing.FontManager.TryGetFontFilePath(familyName, weight, out filePath) &&
            !Drawing.FontManager.TryGetFontFilePath(familyName, FontWeight.Normal, out filePath))
        {
            Console.WriteLine($"Warning: Could not find font file for family '{familyName}' (weight {weight}). Using default.");
            // Fallback to a common system font if possible
            filePath = "C:/Windows/Fonts/arial.ttf"; // Example fallback
        }

        if (filePath == null || !File.Exists(filePath))
        {
            Console.WriteLine($"Warning: Font file '{filePath}' not found or no suitable fallback. Cannot load font.");
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

        TTF.Quit();
        Console.WriteLine("SDL3TextService Cleanup finished.");
    }
}