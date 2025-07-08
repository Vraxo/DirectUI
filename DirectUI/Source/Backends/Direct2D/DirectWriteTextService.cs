// DirectUI/Backends/DirectWriteTextService.cs
using System;
using System.Collections.Generic;
using System.Numerics;
using DirectUI.Core;
using Vortice.DirectWrite;
using Vortice.Mathematics;

namespace DirectUI.Backends;

/// <summary>
/// An implementation of ITextService that uses DirectWrite.
/// It manages caches for text formats, layouts, and measured sizes.
/// </summary>
public class DirectWriteTextService : ITextService
{
    private readonly IDWriteFactory _dwriteFactory;
    private readonly Dictionary<FontKey, IDWriteTextFormat> _textFormatCache = new();
    private readonly Dictionary<TextLayoutCacheKey, ITextLayout> _textLayoutCache = new();
    private readonly Dictionary<(string, FontKey), Vector2> _textSizeCache = new();

    // Internal cache key structs
    internal readonly struct FontKey(ButtonStyle style) : IEquatable<FontKey>
    {
        private readonly string FontName = style.FontName;
        private readonly float FontSize = style.FontSize;
        private readonly FontWeight FontWeight = style.FontWeight;
        private readonly FontStyle FontStyle = style.FontStyle;
        private readonly FontStretch FontStretch = style.FontStretch;
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

    public DirectWriteTextService(IDWriteFactory dwriteFactory)
    {
        _dwriteFactory = dwriteFactory ?? throw new ArgumentNullException(nameof(dwriteFactory));
    }

    public Vector2 MeasureText(string text, ButtonStyle style)
    {
        if (string.IsNullOrEmpty(text) || style is null) return Vector2.Zero;

        var fontKey = new FontKey(style);
        var cacheKey = (text, fontKey);
        if (_textSizeCache.TryGetValue(cacheKey, out var cachedSize)) return cachedSize;

        IDWriteTextFormat? textFormat = GetOrCreateTextFormat(style);
        if (textFormat is null)
        {
            Console.WriteLine("Warning: Failed to create/get TextFormat for measurement.");
            return Vector2.Zero;
        }

        using var textLayout = _dwriteFactory.CreateTextLayout(text, textFormat, float.MaxValue, float.MaxValue);
        TextMetrics textMetrics = textLayout.Metrics;
        var measuredSize = new Vector2(textMetrics.WidthIncludingTrailingWhitespace, textMetrics.Height);
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

        var textFormat = GetOrCreateTextFormat(style);
        if (textFormat is null) return null!; // Should ideally return a null object or throw.

        var dwriteLayout = _dwriteFactory.CreateTextLayout(text, textFormat, maxSize.X, maxSize.Y);
        dwriteLayout.TextAlignment = alignment.Horizontal switch
        {
            HAlignment.Left => TextAlignment.Leading,
            HAlignment.Center => TextAlignment.Center,
            HAlignment.Right => TextAlignment.Trailing,
            _ => TextAlignment.Leading
        };
        dwriteLayout.ParagraphAlignment = alignment.Vertical switch
        {
            VAlignment.Top => ParagraphAlignment.Near,
            VAlignment.Center => ParagraphAlignment.Center,
            VAlignment.Bottom => ParagraphAlignment.Far,
            _ => ParagraphAlignment.Near
        };

        var newLayout = new DirectWriteTextLayout(dwriteLayout, text); // Passed 'text' as the second argument
        _textLayoutCache[layoutKey] = newLayout;
        return newLayout;
    }

    private IDWriteTextFormat? GetOrCreateTextFormat(ButtonStyle style)
    {
        var key = new FontKey(style);
        if (_textFormatCache.TryGetValue(key, out var format))
        {
            return format;
        }

        try
        {
            var newFormat = _dwriteFactory.CreateTextFormat(style.FontName, null, style.FontWeight, style.FontStyle, style.FontStretch, style.FontSize, "en-us");
            if (newFormat is not null)
            {
                _textFormatCache[key] = newFormat;
            }
            return newFormat;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating text format for font '{style.FontName}': {ex.Message}");
            return null;
        }
    }

    public void Cleanup()
    {
        Console.WriteLine("DirectWriteTextService Cleanup: Disposing cached resources...");
        foreach (var pair in _textFormatCache) { pair.Value?.Dispose(); }
        _textFormatCache.Clear();
        foreach (var pair in _textLayoutCache) { pair.Value?.Dispose(); }
        _textLayoutCache.Clear();
        _textSizeCache.Clear();
        Console.WriteLine("DirectWriteTextService Cleanup finished.");
    }
}