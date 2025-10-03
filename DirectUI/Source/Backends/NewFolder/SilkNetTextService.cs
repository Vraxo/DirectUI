using System;
using System.Collections.Generic;
using System.Numerics;
using DirectUI.Core;
using SkiaSharp;
using Vortice.DirectWrite; // For FontWeight, etc.

namespace DirectUI.Backends.SkiaSharp;

public class SilkNetTextService : ITextService
{
    private readonly Dictionary<TypefaceKey, SKTypeface> _typefaceCache = new();
    private readonly Dictionary<TextLayoutCacheKey, ITextLayout> _textLayoutCache = new();
    private readonly Dictionary<(string, MeasurementKey), Vector2> _textSizeCache = new();

    // Key for size-INDEPENDENT SKTypeface objects
    internal readonly struct TypefaceKey : IEquatable<TypefaceKey>
    {
        private readonly string FontName;
        private readonly FontWeight FontWeight;
        private readonly FontStyle FontStyle;

        public TypefaceKey(ButtonStyle style)
        {
            FontName = style.FontName;
            FontWeight = style.FontWeight;
            FontStyle = style.FontStyle;
        }

        public bool Equals(TypefaceKey other) => FontName == other.FontName && FontWeight == other.FontWeight && FontStyle == other.FontStyle;
        public override bool Equals(object? obj) => obj is TypefaceKey other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(FontName, FontWeight, FontStyle);
    }

    // Key for size-DEPENDENT measurements
    private readonly struct MeasurementKey : IEquatable<MeasurementKey>
    {
        private readonly string FontName;
        private readonly FontWeight FontWeight;
        private readonly FontStyle FontStyle;
        private readonly float FontSize;

        public MeasurementKey(ButtonStyle style)
        {
            FontName = style.FontName;
            FontWeight = style.FontWeight;
            FontStyle = style.FontStyle;
            FontSize = style.FontSize;
        }

        public bool Equals(MeasurementKey other) => FontName == other.FontName && FontWeight == other.FontWeight && FontStyle == other.FontStyle && FontSize.Equals(other.FontSize);
        public override bool Equals(object? obj) => obj is MeasurementKey other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(FontName, FontWeight, FontStyle, FontSize);
    }

    private readonly struct TextLayoutCacheKey : IEquatable<TextLayoutCacheKey>
    {
        private readonly string Text;
        private readonly MeasurementKey FontKey; // Use the more detailed key
        public TextLayoutCacheKey(string text, ButtonStyle style)
        {
            Text = text;
            FontKey = new MeasurementKey(style);
        }
        public bool Equals(TextLayoutCacheKey other) => Text == other.Text && FontKey.Equals(other.FontKey);
        public override bool Equals(object? obj) => obj is TextLayoutCacheKey other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Text, FontKey);
    }

    public SKTypeface GetOrCreateTypeface(ButtonStyle style)
    {
        var typefaceKey = new TypefaceKey(style);
        if (_typefaceCache.TryGetValue(typefaceKey, out var typeface))
        {
            return typeface;
        }

        SKFontStyleWeight weight = (SKFontStyleWeight)style.FontWeight;
        SKFontStyleSlant slant = style.FontStyle == FontStyle.Italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright;

        typeface = SKTypeface.FromFamilyName(style.FontName, weight, SKFontStyleWidth.Normal, slant);
        _typefaceCache[typefaceKey] = typeface;
        return typeface;
    }

    public Vector2 MeasureText(string text, ButtonStyle style)
    {
        if (string.IsNullOrEmpty(text)) return Vector2.Zero;

        var measurementKey = new MeasurementKey(style);
        var cacheKey = (text, measurementKey);
        if (_textSizeCache.TryGetValue(cacheKey, out var cachedSize))
        {
            return cachedSize;
        }

        // Use the full layout logic for accurate measurement with fallbacks
        var layout = GetTextLayout(text, style, new Vector2(float.MaxValue, float.MaxValue), new Alignment());
        var measuredSize = layout.Size;

        _textSizeCache[cacheKey] = measuredSize;
        return measuredSize;
    }

    public ITextLayout GetTextLayout(string text, ButtonStyle style, Vector2 maxSize, Alignment alignment)
    {
        // Note: For simplicity, this implementation doesn't use maxSize/alignment in the layout
        // creation itself, as breaking into runs makes wrapping complex. The renderer applies alignment.
        var key = new TextLayoutCacheKey(text, style);
        if (_textLayoutCache.TryGetValue(key, out var layout))
        {
            return layout;
        }

        var newLayout = new SilkNetTextLayout(text, style, this);
        _textLayoutCache[key] = newLayout;
        return newLayout;
    }

    public void Cleanup()
    {
        foreach (var typeface in _typefaceCache.Values)
        {
            typeface.Dispose();
        }
        _typefaceCache.Clear();
        _textLayoutCache.Clear();
        _textSizeCache.Clear();
    }
}