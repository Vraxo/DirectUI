using System;
using System.Collections.Generic;
using System.Numerics;
using DirectUI.Core;
using SkiaSharp;
using Typography.OpenFont;
using Vortice.DirectWrite; // For FontWeight, etc.

namespace DirectUI.Backends.SkiaSharp;

public class SilkNetTextService : ITextService
{
    private readonly Dictionary<FontKey, SKTypeface> _typefaceCache = new();
    private readonly Dictionary<TextLayoutCacheKey, ITextLayout> _textLayoutCache = new();
    private readonly Dictionary<(string, FontKey), Vector2> _textSizeCache = new();

    private readonly struct FontKey : IEquatable<FontKey>
    {
        private readonly string FontName;
        private readonly FontWeight FontWeight;
        private readonly FontStyle FontStyle;

        public FontKey(ButtonStyle style)
        {
            FontName = style.FontName;
            FontWeight = style.FontWeight;
            FontStyle = style.FontStyle;
        }

        public bool Equals(FontKey other) => FontName == other.FontName && FontWeight == other.FontWeight && FontStyle == other.FontStyle;
        public override bool Equals(object? obj) => obj is FontKey other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(FontName, FontWeight, FontStyle);
    }
    private readonly struct TextLayoutCacheKey : IEquatable<TextLayoutCacheKey>
    {
        private readonly string Text;
        private readonly FontKey FontKey;
        private readonly float FontSize;
        public TextLayoutCacheKey(string text, ButtonStyle style)
        {
            Text = text;
            FontKey = new FontKey(style);
            FontSize = style.FontSize;
        }
        public bool Equals(TextLayoutCacheKey other) => Text == other.Text && FontKey.Equals(other.FontKey) && FontSize.Equals(other.FontSize);
        public override bool Equals(object? obj) => obj is TextLayoutCacheKey other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Text, FontKey, FontSize);
    }
    public SKTypeface GetOrCreateTypeface(ButtonStyle style)
    {
        var key = new FontKey(style);
        if (_typefaceCache.TryGetValue(key, out var typeface))
        {
            return typeface;
        }

        SKFontStyleWeight weight = (SKFontStyleWeight)style.FontWeight;
        SKFontStyleSlant slant = style.FontStyle == FontStyle.Italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright;

        typeface = SKTypeface.FromFamilyName(style.FontName, weight, SKFontStyleWidth.Normal, slant);
        _typefaceCache[key] = typeface;
        return typeface;
    }
    public Vector2 MeasureText(string text, ButtonStyle style)
    {
        if (string.IsNullOrEmpty(text)) return Vector2.Zero;

        var typeface = GetOrCreateTypeface(style);
        using var font = new SKFont(typeface, style.FontSize);
        using var paint = new SKPaint(font);
        var rect = new SKRect();
        paint.MeasureText(text, ref rect);
        return new Vector2(rect.Width, rect.Height);
    }

    public ITextLayout GetTextLayout(string text, ButtonStyle style, Vector2 maxSize, Alignment alignment)
    {
        var key = new TextLayoutCacheKey(text, style);
        if (_textLayoutCache.TryGetValue(key, out var layout))
        {
            return layout;
        }

        var typeface = GetOrCreateTypeface(style);
        var newLayout = new SilkNetTextLayout(text, typeface, style);
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