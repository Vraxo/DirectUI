using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using DirectUI.Core;
using DirectUI.Drawing;
using SkiaSharp;
using SkiaSharp.HarfBuzz;
using Vortice.Direct2D1;
using Vortice.Mathematics;

namespace DirectUI.Backends.SkiaSharp;

public class SilkNetRenderer : IRenderer
{
    private SKCanvas? _canvas;
    private Vector2 _renderTargetSize;
    private readonly SilkNetTextService _textService;
    private readonly Dictionary<string, SKBitmap> _bitmapCache = new();

    public Vector2 RenderTargetSize => _renderTargetSize;

    public SilkNetRenderer(SilkNetTextService textService)
    {
        _textService = textService;
    }

    public void SetCanvas(SKCanvas canvas, Vector2 size)
    {
        _canvas = canvas;
        _renderTargetSize = size;
    }

    public void DrawLine(Vector2 p1, Vector2 p2, Color color, float strokeWidth)
    {
        if (_canvas is null) return;
        using var paint = new SKPaint
        {
            Color = new SKColor(color.R, color.G, color.B, color.A),
            StrokeWidth = strokeWidth,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke
        };
        _canvas.DrawLine(p1.X, p1.Y, p2.X, p2.Y, paint);
    }

    public void DrawBox(Rect rect, BoxStyle style)
    {
        if (_canvas is null || style is null || rect.Width <= 0 || rect.Height <= 0) return;

        var skRect = new SKRect(rect.Left, rect.Top, rect.Right, rect.Bottom);
        float maxRadius = Math.Min(rect.Width, rect.Height) / 2f;
        float radius = Math.Clamp(style.Roundness * maxRadius, 0, maxRadius);

        // Draw fill
        if (style.FillColor.A > 0)
        {
            using var fillPaint = new SKPaint
            {
                Color = new SKColor(style.FillColor.R, style.FillColor.G, style.FillColor.B, style.FillColor.A),
                IsAntialias = true,
                Style = SKPaintStyle.Fill
            };
            _canvas.DrawRoundRect(skRect, radius, radius, fillPaint);
        }

        // Draw border
        if (style.BorderColor.A > 0 && style.BorderLength > 0)
        {
            using var borderPaint = new SKPaint
            {
                Color = new SKColor(style.BorderColor.R, style.BorderColor.G, style.BorderColor.B, style.BorderColor.A),
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = style.BorderLength
            };
            // Inset the rectangle slightly so the stroke is drawn on the edge, not half-in/half-out
            skRect.Inflate(-style.BorderLength / 2f, -style.BorderLength / 2f);
            _canvas.DrawRoundRect(skRect, radius, radius, borderPaint);
        }
    }

    public void DrawText(Vector2 origin, string text, ButtonStyle style, Alignment alignment, Vector2 maxSize, Color color)
    {
        if (_canvas is null || string.IsNullOrEmpty(text)) return;

        var layout = _textService.GetTextLayout(text, style, maxSize, alignment) as SilkNetTextLayout;
        if (layout is null || !layout.Runs.Any()) return;

        var totalSize = layout.Size;
        var textDrawPos = origin;

        // --- Horizontal alignment ---
        if (maxSize.X > 0)
        {
            switch (alignment.Horizontal)
            {
                case HAlignment.Center:
                    textDrawPos.X += (maxSize.X - totalSize.X) / 2f;
                    break;
                case HAlignment.Right:
                    textDrawPos.X += maxSize.X - totalSize.X;
                    break;
            }
        }

        // Use the metrics of the first (primary) font for vertical alignment to ensure consistency.
        var primaryMetrics = layout.Runs.First().FontMetrics;
        var baselineY = origin.Y;

        if (maxSize.Y > 0)
        {
            switch (alignment.Vertical)
            {
                case VAlignment.Top:
                    baselineY -= primaryMetrics.Ascent;
                    break;
                case VAlignment.Center:
                    float fontHeight = primaryMetrics.Descent - primaryMetrics.Ascent;
                    baselineY += (maxSize.Y - fontHeight) / 2f - primaryMetrics.Ascent;
                    baselineY -= 1.5f; // Visual centering correction
                    break;
                case VAlignment.Bottom:
                    baselineY += maxSize.Y - primaryMetrics.Descent;
                    break;
            }
        }
        else
        {
            baselineY -= primaryMetrics.Ascent;
        }

        var currentX = textDrawPos.X;
        var skColor = new SKColor(color.R, color.G, color.B, color.A);

        foreach (var run in layout.Runs)
        {
            using var font = new SKFont(run.Typeface, style.FontSize);
            using var paint = new SKPaint(font) { Color = skColor, IsAntialias = true };
            using var shaper = new SKShaper(run.Typeface);

            _canvas.DrawShapedText(shaper, run.Text, currentX, baselineY, paint);
            currentX += run.Size.X;
        }
    }

    public void DrawImage(byte[] imageData, string imageKey, Rect destination)
    {
        if (_canvas is null || imageData is null || imageData.Length == 0) return;

        if (!_bitmapCache.TryGetValue(imageKey, out var bitmap))
        {
            try
            {
                bitmap = SKBitmap.Decode(imageData);
                if (bitmap is not null)
                {
                    _bitmapCache[imageKey] = bitmap;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to decode image with key {imageKey}: {ex.Message}");
                // Cache a null to prevent re-trying every frame
                _bitmapCache[imageKey] = null!;
                return;
            }
        }

        if (bitmap is null) return; // Decoding failed previously or image is invalid

        var destRect = new SKRect(destination.Left, destination.Top, destination.Right, destination.Bottom);

        // Use an SKPaint object with high-quality filtering to ensure the stretched image is blurry, not pixelated.
        using var paint = new SKPaint { FilterQuality = SKFilterQuality.High };
        _canvas.DrawBitmap(bitmap, destRect, paint);
    }

    public void PushClipRect(Rect rect, AntialiasMode antialiasMode)
    {
        _canvas?.Save();
        _canvas?.ClipRect(new SKRect(rect.Left, rect.Top, rect.Right, rect.Bottom), SKClipOperation.Intersect, true);
    }

    public void PopClipRect()
    {
        _canvas?.Restore();
    }

    public void Flush()
    {
        _canvas?.Flush();
    }

    public void Cleanup()
    {
        // Canvas is managed by the host
        _canvas = null;

        // Dispose cached bitmaps
        foreach (var bitmap in _bitmapCache.Values)
        {
            bitmap?.Dispose();
        }
        _bitmapCache.Clear();
    }
}