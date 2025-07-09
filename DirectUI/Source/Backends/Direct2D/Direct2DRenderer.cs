using System.Numerics;
using DirectUI.Core;
using SharpGen.Runtime;
using Vortice.Direct2D1;
using Vortice.DirectWrite;

namespace DirectUI.Backends;

/// <summary>
/// A rendering backend that uses Direct2D to implement the IRenderer interface.
/// It manages its own cache of Direct2D brushes and text layouts.
/// </summary>
public class Direct2DRenderer : IRenderer
{
    private readonly ID2D1RenderTarget _renderTarget;
    private readonly IDWriteFactory _dwriteFactory; // Added DirectWrite factory
    private readonly Dictionary<Color, ID2D1SolidColorBrush> _brushCache = new();

    // Internal text layout cache for DrawText method
    private readonly Dictionary<TextLayoutCacheKey, IDWriteTextLayout> _textLayoutCache = new();
    private readonly Dictionary<FontKey, IDWriteTextFormat> _textFormatCache = new(); // Added text format cache

    // Internal cache key for text layouts (similar to UIResources.TextLayoutCacheKey)
    private readonly struct TextLayoutCacheKey : IEquatable<TextLayoutCacheKey>
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

    private readonly struct FontKey : IEquatable<FontKey>
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


    public Vector2 RenderTargetSize => new(_renderTarget.Size.Width, _renderTarget.Size.Height);

    public Direct2DRenderer(ID2D1RenderTarget renderTarget, IDWriteFactory dwriteFactory) // Added dwriteFactory
    {
        _renderTarget = renderTarget ?? throw new ArgumentNullException(nameof(renderTarget));
        _dwriteFactory = dwriteFactory ?? throw new ArgumentNullException(nameof(dwriteFactory));
    }

    public void DrawLine(Vector2 p1, Vector2 p2, Drawing.Color color, float strokeWidth)
    {
        var brush = GetOrCreateBrush(color);
        if (brush is null) return;
        _renderTarget.DrawLine(p1, p2, brush, strokeWidth);
    }

    public void DrawBox(Vortice.Mathematics.Rect rect, BoxStyle style)
    {
        if (_renderTarget is null || style is null || rect.Width <= 0 || rect.Height <= 0) return;

        var pos = rect.TopLeft;
        var size = new Vector2(rect.Width, rect.Height);

        ID2D1SolidColorBrush? fillBrush = GetOrCreateBrush(style.FillColor);
        ID2D1SolidColorBrush? borderBrush = GetOrCreateBrush(style.BorderColor);

        float borderTop = Math.Max(0f, style.BorderLengthTop);
        float borderRight = Math.Max(0f, style.BorderLengthRight);
        float borderBottom = Math.Max(0f, style.BorderLengthBottom);
        float borderLeft = Math.Max(0f, style.BorderLengthLeft);

        bool hasVisibleFill = style.FillColor.A > 0 && fillBrush is not null;
        bool hasVisibleBorder = style.BorderColor.A > 0 && borderBrush is not null && (borderTop > 0 || borderRight > 0 || borderBottom > 0 || borderLeft > 0);

        if (!hasVisibleFill && !hasVisibleBorder) return;

        if (style.Roundness > 0.0f)
        {
            Vortice.Mathematics.Rect outerBounds = new Vortice.Mathematics.Rect(pos.X, pos.Y, size.X, size.Y);
            float maxRadius = Math.Min(outerBounds.Width * 0.5f, outerBounds.Height * 0.5f);
            float radius = Math.Max(0f, maxRadius * Math.Clamp(style.Roundness, 0.0f, 1.0f));

            if (float.IsFinite(radius) && radius >= 0)
            {
                if (hasVisibleBorder)
                {
                    System.Drawing.RectangleF outerRectF = new(outerBounds.X, outerBounds.Y, outerBounds.Width, outerBounds.Height);
                    _renderTarget.FillRoundedRectangle(new RoundedRectangle(outerRectF, radius, radius), borderBrush);
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
                        _renderTarget.FillRoundedRectangle(new RoundedRectangle(fillRectF, innerRadiusX, innerRadiusY), fillBrush);
                    }
                    else if (!hasVisibleBorder && fillBrush is not null)
                    {
                        System.Drawing.RectangleF outerRectF = new(outerBounds.X, outerBounds.Y, outerBounds.Width, outerBounds.Height);
                        _renderTarget.FillRoundedRectangle(new RoundedRectangle(outerRectF, radius, radius), fillBrush);
                    }
                }
                return;
            }
        }
        if (hasVisibleBorder && borderBrush is not null)
        {
            _renderTarget.FillRectangle(rect, borderBrush);
        }
        if (hasVisibleFill)
        {
            float fillX = pos.X + borderLeft;
            float fillY = pos.Y + borderTop;
            float fillWidth = Math.Max(0f, size.X - borderLeft - borderRight);
            float fillHeight = Math.Max(0f, size.Y - borderTop - borderBottom);
            if (fillWidth > 0 && fillHeight > 0)
            {
                _renderTarget.FillRectangle(new Vortice.Mathematics.Rect(fillX, fillY, fillWidth, fillHeight), fillBrush);
            }
            else if (!hasVisibleBorder && fillBrush is not null)
            {
                _renderTarget.FillRectangle(rect, fillBrush);
            }
        }
    }

    public void DrawText(Vector2 origin, string text, ButtonStyle style, Alignment alignment, Vector2 maxSize, Drawing.Color color)
    {
        if (string.IsNullOrEmpty(text)) return;

        var textBrush = GetOrCreateBrush(color);
        if (textBrush is null) return;

        var layoutKey = new TextLayoutCacheKey(text, style, maxSize, alignment);
        if (!_textLayoutCache.TryGetValue(layoutKey, out var textLayout))
        {
            var textFormat = GetOrCreateTextFormat(style);
            if (textFormat is null) return;

            textLayout = _dwriteFactory.CreateTextLayout(text, textFormat, maxSize.X, maxSize.Y);
            textLayout.TextAlignment = alignment.Horizontal switch
            {
                HAlignment.Left => Vortice.DirectWrite.TextAlignment.Leading,
                HAlignment.Center => Vortice.DirectWrite.TextAlignment.Center,
                HAlignment.Right => Vortice.DirectWrite.TextAlignment.Trailing,
                _ => Vortice.DirectWrite.TextAlignment.Leading
            };
            textLayout.ParagraphAlignment = alignment.Vertical switch
            {
                VAlignment.Top => ParagraphAlignment.Near,
                VAlignment.Center => ParagraphAlignment.Center,
                VAlignment.Bottom => ParagraphAlignment.Far,
                _ => ParagraphAlignment.Near
            };
            _textLayoutCache[layoutKey] = textLayout;
        }

        // A small vertical adjustment to compensate for font metrics making text appear slightly too low when using ParagraphAlignment.Center.
        float yOffsetCorrection = (alignment.Vertical == VAlignment.Center) ? -1.5f : 0f;

        _renderTarget.DrawTextLayout(new Vector2(origin.X, origin.Y + yOffsetCorrection), textLayout, textBrush, Vortice.Direct2D1.DrawTextOptions.None);
    }

    public void PushClipRect(Vortice.Mathematics.Rect rect, AntialiasMode antialiasMode)
    {
        _renderTarget.PushAxisAlignedClip(rect, antialiasMode);
    }

    public void PopClipRect()
    {
        _renderTarget.PopAxisAlignedClip();
    }

    public void Cleanup()
    {
        foreach (var pair in _brushCache)
        {
            pair.Value?.Dispose();
        }
        _brushCache.Clear();

        foreach (var pair in _textLayoutCache)
        {
            pair.Value?.Dispose();
        }
        _textLayoutCache.Clear();

        foreach (var pair in _textFormatCache) // Dispose text formats
        {
            pair.Value?.Dispose();
        }
        _textFormatCache.Clear();
    }

    private ID2D1SolidColorBrush? GetOrCreateBrush(Drawing.Color color)
    {
        if (_renderTarget is null)
        {
            Console.WriteLine("Error: GetOrCreateBrush called with no active render target.");
            return null;
        }

        if (_brushCache.TryGetValue(color, out var brush) && brush is not null)
        {
            return brush;
        }

        if (brush is null && _brushCache.ContainsKey(color))
        {
            _brushCache.Remove(color);
        }

        try
        {
            Vortice.Mathematics.Color4 color4 = color;
            brush = _renderTarget.CreateSolidColorBrush(color4);
            if (brush is not null)
            {
                _brushCache[color] = brush;
                return brush;
            }

            Console.WriteLine($"Warning: CreateSolidColorBrush returned null for color {color}");
            return null;
        }
        catch (SharpGenException ex) when (ex.ResultCode.Code == Vortice.Direct2D1.ResultCode.RecreateTarget.Code)
        {
            Console.WriteLine($"Brush creation failed due to RecreateTarget error (Color: {color}). External cleanup needed.");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating brush for color {color}: {ex.Message}");
            return null;
        }
    }

    private IDWriteTextFormat? GetOrCreateTextFormat(ButtonStyle style)
    {
        if (_dwriteFactory is null) return null;

        var key = new FontKey(style);
        if (_textFormatCache.TryGetValue(key, out var format)) // Use the member field
        {
            return format;
        }

        try
        {
            var newFormat = _dwriteFactory.CreateTextFormat(style.FontName, null, style.FontWeight, style.FontStyle, style.FontStretch, style.FontSize, "en-us");
            if (newFormat is not null) { _textFormatCache[key] = newFormat; } // Add to the member field
            return newFormat;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating text format for font '{style.FontName}': {ex.Message}");
            return null;
        }
    }
}
