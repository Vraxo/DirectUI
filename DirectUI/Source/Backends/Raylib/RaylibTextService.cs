// DirectUI/Backends/Raylib/RaylibTextService.cs
using System;
using System.Collections.Generic;
using System.Numerics;
using DirectUI.Core;
using DirectUI.Drawing;
using Vortice.Mathematics; // For Color4, Rect
using Raylib_cs; // Raylib specific library

namespace DirectUI.Backends;

/// <summary>
/// An implementation of ITextService that uses Raylib.
/// It manages caches for text layouts.
/// </summary>
public class RaylibTextService : ITextService
{
    // This factor compensates for the perceptual size difference between DirectWrite and FreeType rendering.
    // DirectWrite text tends to appear slightly larger/bolder for the same point size.
    private const float FONT_SCALE_FACTOR = 1.125f;

    private readonly Dictionary<TextLayoutCacheKey, ITextLayout> _textLayoutCache = new();
    private readonly Dictionary<(string, FontKey), Vector2> _textSizeCache = new();

    // Internal cache key structs (re-defined or referenced from a common place)
    internal readonly struct FontKey(ButtonStyle style) : IEquatable<FontKey>
    {
        private readonly string FontName = style.FontName;
        private readonly float FontSize = style.FontSize;
        private readonly Vortice.DirectWrite.FontWeight FontWeight = style.FontWeight;
        private readonly Vortice.DirectWrite.FontStyle FontStyle = style.FontStyle;
        private readonly Vortice.DirectWrite.FontStretch FontStretch = style.FontStretch;
        public bool Equals(FontKey other) => FontName == other.FontName && FontSize.Equals(other.FontSize) && FontWeight == other.FontWeight && FontStyle == other.FontStyle && FontStretch == other.FontStretch;
        public override bool Equals(object? obj) => obj is FontKey other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(FontName, FontSize, FontWeight, FontStyle, FontStretch);
    }

    internal readonly struct TextLayoutCacheKey(string text, ButtonStyle style, Vector2 maxSize, Alignment alignment) : IEquatable<TextLayoutCacheKey>
    {
        private readonly string Text = text;
        private readonly FontKey FontKey = new(style);
        private readonly Vector2 MaxSize = maxSize;
        private readonly HAlignment HAlign = alignment.Horizontal;
        private readonly VAlignment VAlign = alignment.Vertical;
        public bool Equals(TextLayoutCacheKey other) => Text == other.Text && MaxSize.Equals(other.MaxSize) && HAlign == other.HAlign && VAlign == other.VAlign && FontKey.Equals(other.FontKey);
        public override bool Equals(object? obj) => obj is TextLayoutCacheKey other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Text, FontKey, MaxSize, HAlign, VAlign);
    }

    public RaylibTextService()
    {
        // No specific Raylib font factory initialization needed here
    }

    public Vector2 MeasureText(string text, ButtonStyle style)
    {
        if (string.IsNullOrEmpty(text) || style is null) return Vector2.Zero;

        var fontKey = new FontKey(style);
        var cacheKey = (text, fontKey);
        if (_textSizeCache.TryGetValue(cacheKey, out var cachedSize)) return cachedSize;

        float finalFontSize = style.FontSize * FONT_SCALE_FACTOR;
        int atlasSize = (int)Math.Round(finalFontSize);
        if (atlasSize <= 0) atlasSize = 1;

        // Use the FontManager to get the appropriate font at the native resolution and correct weight.
        Font rlFont = FontManager.GetFont(style.FontName, atlasSize, style.FontWeight);

        // Measure using the final (compensated) float font size for accurate layout metrics.
        Vector2 measuredSize = Raylib.MeasureTextEx(rlFont, text, finalFontSize, finalFontSize / 10f);
        _textSizeCache[cacheKey] = measuredSize;
        return measuredSize;
    }

    public ITextLayout GetTextLayout(string text, ButtonStyle style, Vector2 maxSize, Alignment alignment)
    {
        var layoutKey = new TextLayoutCacheKey(text, style, maxSize, alignment);
        if (_textLayoutCache.TryGetValue(layoutKey, out var cachedLayout))
        {
            return cachedLayout;
        }

        float finalFontSize = style.FontSize * FONT_SCALE_FACTOR;
        int atlasSize = (int)Math.Round(finalFontSize);
        if (atlasSize <= 0) atlasSize = 1;

        // Fetch the font at the correct, native atlas resolution and weight.
        var font = FontManager.GetFont(style.FontName, atlasSize, style.FontWeight);

        // Pass the pre-loaded font and the final size to the layout constructor.
        var newLayout = new RaylibTextLayout(text, font, finalFontSize);
        _textLayoutCache[layoutKey] = newLayout;
        return newLayout;
    }

    public void Cleanup()
    {
        Console.WriteLine("RaylibTextService Cleanup: Clearing cached resources...");
        foreach (var pair in _textLayoutCache) { pair.Value?.Dispose(); } // Dispose the ITextLayout instances
        _textLayoutCache.Clear();
        _textSizeCache.Clear();
        Console.WriteLine("RaylibTextService Cleanup finished.");
    }
}