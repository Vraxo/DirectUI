using SharpGen.Runtime;
using System;
using System.Collections.Generic;
using System.Numerics;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;

namespace DirectUI;

/// <summary>
/// Manages the creation, caching, and cleanup of shareable graphics resources
/// like brushes and text formats to avoid recreating them every frame.
/// </summary>
public class UIResources
{
    // --- Font Caching Key ---
    internal readonly struct FontKey : IEquatable<FontKey>
    {
        public readonly string FontName;
        public readonly float FontSize;
        public readonly FontWeight FontWeight;
        public readonly FontStyle FontStyle;
        public readonly FontStretch FontStretch;

        public FontKey(ButtonStyle style)
        {
            FontName = style.FontName;
            FontSize = style.FontSize;
            FontWeight = style.FontWeight;
            FontStyle = style.FontStyle;
            FontStretch = style.FontStretch;
        }

        public bool Equals(FontKey other) => FontName == other.FontName && FontSize.Equals(other.FontSize) && FontWeight == other.FontWeight && FontStyle == other.FontStyle && FontStretch == other.FontStretch;
        public override bool Equals(object? obj) => obj is FontKey other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(FontName, FontSize, FontWeight, FontStyle, FontStretch);
    }

    // --- Text Layout Caching Key ---
    internal readonly struct TextLayoutCacheKey : IEquatable<TextLayoutCacheKey>
    {
        public readonly string Text;
        public readonly FontKey FontKey;
        public readonly Vector2 MaxSize;
        public readonly HAlignment HAlign;
        public readonly VAlignment VAlign;

        public TextLayoutCacheKey(string text, ButtonStyle style, Vector2 maxSize, Alignment alignment)
        {
            Text = text;
            FontKey = new FontKey(style);
            MaxSize = maxSize;
            HAlign = alignment.Horizontal;
            VAlign = alignment.Vertical;
        }

        public bool Equals(TextLayoutCacheKey other) => Text == other.Text && MaxSize.Equals(other.MaxSize) && HAlign == other.HAlign && VAlign == other.VAlign && FontKey.Equals(other.FontKey);
        public override bool Equals(object? obj) => obj is TextLayoutCacheKey other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Text, FontKey, MaxSize, HAlign, VAlign);
    }

    // --- Caches ---
    private readonly Dictionary<Color4, ID2D1SolidColorBrush> brushCache = new();
    private readonly Dictionary<FontKey, IDWriteTextFormat> textFormatCache = new();
    private readonly Dictionary<(string, FontKey), Vector2> textSizeCache = new();
    internal readonly Dictionary<TextLayoutCacheKey, IDWriteTextLayout> textLayoutCache = new();

    // --- Brush and Font Cache ---
    public ID2D1SolidColorBrush GetOrCreateBrush(ID2D1RenderTarget renderTarget, Color4 color)
    {
        if (renderTarget is null) { Console.WriteLine("Error: GetOrCreateBrush called with no active render target."); return null!; }
        if (brushCache.TryGetValue(color, out ID2D1SolidColorBrush? brush) && brush is not null) { return brush; }
        else if (brush is null && brushCache.ContainsKey(color)) { brushCache.Remove(color); }
        try
        {
            brush = renderTarget.CreateSolidColorBrush(color);
            if (brush is not null) { brushCache[color] = brush; return brush; }
            else { Console.WriteLine($"Warning: CreateSolidColorBrush returned null for color {color}"); return null!; }
        }
        catch (SharpGenException ex) when (ex.ResultCode.Code == ResultCode.RecreateTarget.Code) { Console.WriteLine($"Brush creation failed due to RecreateTarget error (Color: {color}). External cleanup needed."); return null!; }
        catch (Exception ex) { Console.WriteLine($"Error creating brush for color {color}: {ex.Message}"); return null!; }
    }

    public IDWriteTextFormat? GetOrCreateTextFormat(IDWriteFactory dwriteFactory, ButtonStyle style)
    {
        if (dwriteFactory is null) return null;

        var key = new FontKey(style);
        if (textFormatCache.TryGetValue(key, out var format))
        {
            return format;
        }

        try
        {
            var newFormat = dwriteFactory.CreateTextFormat(style.FontName, null, style.FontWeight, style.FontStyle, style.FontStretch, style.FontSize, "en-us");
            if (newFormat is not null) { textFormatCache[key] = newFormat; }
            return newFormat;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating text format for font '{style.FontName}': {ex.Message}");
            return null;
        }
    }

    public Vector2 MeasureText(IDWriteFactory dwriteFactory, string text, ButtonStyle style)
    {
        if (string.IsNullOrEmpty(text) || style is null) return Vector2.Zero;

        var fontKey = new FontKey(style);
        var cacheKey = (text, fontKey);
        if (textSizeCache.TryGetValue(cacheKey, out var cachedSize)) return cachedSize;

        IDWriteTextFormat? textFormat = GetOrCreateTextFormat(dwriteFactory, style);
        if (textFormat is null) { Console.WriteLine("Warning: Failed to create/get TextFormat for measurement."); return Vector2.Zero; }

        using var textLayout = dwriteFactory.CreateTextLayout(text, textFormat, float.MaxValue, float.MaxValue);
        TextMetrics textMetrics = textLayout.Metrics;
        var measuredSize = new Vector2(textMetrics.WidthIncludingTrailingWhitespace, textMetrics.Height);
        textSizeCache[cacheKey] = measuredSize;
        return measuredSize;
    }

    public void DrawBoxStyleHelper(ID2D1RenderTarget renderTarget, Vector2 pos, Vector2 size, BoxStyle style)
    {
        if (renderTarget is null || style is null || size.X <= 0 || size.Y <= 0) return;

        ID2D1SolidColorBrush fillBrush = GetOrCreateBrush(renderTarget, style.FillColor);
        ID2D1SolidColorBrush borderBrush = GetOrCreateBrush(renderTarget, style.BorderColor);

        float borderTop = Math.Max(0f, style.BorderLengthTop);
        float borderRight = Math.Max(0f, style.BorderLengthRight);
        float borderBottom = Math.Max(0f, style.BorderLengthBottom);
        float borderLeft = Math.Max(0f, style.BorderLengthLeft);

        bool hasVisibleFill = style.FillColor.A > 0 && fillBrush is not null;
        bool hasVisibleBorder = style.BorderColor.A > 0 && borderBrush is not null && (borderTop > 0 || borderRight > 0 || borderBottom > 0 || borderLeft > 0);

        if (!hasVisibleFill && !hasVisibleBorder) return;

        if (style.Roundness > 0.0f)
        {
            Rect outerBounds = new Rect(pos.X, pos.Y, size.X, size.Y);
            float maxRadius = Math.Min(outerBounds.Width * 0.5f, outerBounds.Height * 0.5f);
            float radius = Math.Max(0f, maxRadius * Math.Clamp(style.Roundness, 0.0f, 1.0f));

            if (float.IsFinite(radius) && radius >= 0)
            {
                if (hasVisibleBorder)
                {
                    System.Drawing.RectangleF outerRectF = new(outerBounds.X, outerBounds.Y, outerBounds.Width, outerBounds.Height);
                    renderTarget.FillRoundedRectangle(new RoundedRectangle(outerRectF, radius, radius), borderBrush);
                }
                if (hasVisibleFill)
                {
                    float fillX = pos.X + borderLeft;
                    float fillY = pos.Y + borderTop;
                    float fillWidth = Math.Max(0f, size.X - borderLeft - borderRight);
                    float fillHeight = Math.Max(0f, size.Y - borderTop - borderBottom);
                    if (fillWidth > 0 && fillHeight > 0)
                    {
                        float avgBorderX = (borderLeft + borderRight) * 0.5f;
                        float avgBorderY = (borderTop + borderBottom) * 0.5f;
                        float innerRadiusX = Math.Max(0f, radius - avgBorderX);
                        float innerRadiusY = Math.Max(0f, radius - avgBorderY);
                        System.Drawing.RectangleF fillRectF = new(fillX, fillY, fillWidth, fillHeight);
                        renderTarget.FillRoundedRectangle(new RoundedRectangle(fillRectF, innerRadiusX, innerRadiusY), fillBrush);
                    }
                    else if (!hasVisibleBorder && fillBrush is not null)
                    {
                        System.Drawing.RectangleF outerRectF = new(outerBounds.X, outerBounds.Y, outerBounds.Width, outerBounds.Height);
                        renderTarget.FillRoundedRectangle(new RoundedRectangle(outerRectF, radius, radius), fillBrush);
                    }
                }
                return;
            }
        }
        if (hasVisibleBorder && borderBrush is not null)
        {
            renderTarget.FillRectangle(new Rect(pos.X, pos.Y, size.X, size.Y), borderBrush);
        }
        if (hasVisibleFill)
        {
            float fillX = pos.X + borderLeft;
            float fillY = pos.Y + borderTop;
            float fillWidth = Math.Max(0f, size.X - borderLeft - borderRight);
            float fillHeight = Math.Max(0f, size.Y - borderTop - borderBottom);
            if (fillWidth > 0 && fillHeight > 0)
            {
                renderTarget.FillRectangle(new Rect(fillX, fillY, fillWidth, fillHeight), fillBrush);
            }
            else if (!hasVisibleBorder && fillBrush is not null)
            {
                renderTarget.FillRectangle(new Rect(pos.X, pos.Y, size.X, size.Y), fillBrush);
            }
        }
    }

    public void CleanupResources()
    {
        Console.WriteLine("UI Resource Cleanup: Disposing cached resources...");
        int brushCount = brushCache.Count;
        foreach (var pair in brushCache) { pair.Value?.Dispose(); }
        brushCache.Clear();

        int formatCount = textFormatCache.Count;
        foreach (var pair in textFormatCache) { pair.Value?.Dispose(); }
        textFormatCache.Clear();

        int layoutCount = textLayoutCache.Count;
        foreach (var pair in textLayoutCache) { pair.Value?.Dispose(); }
        textLayoutCache.Clear();

        int sizeCacheCount = textSizeCache.Count;
        textSizeCache.Clear();

        Console.WriteLine($"UI Resource Cleanup finished. Disposed {brushCount} brushes, {formatCount} text formats, and {layoutCount} text layouts. Cleared {sizeCacheCount} size cache entries.");
    }
}