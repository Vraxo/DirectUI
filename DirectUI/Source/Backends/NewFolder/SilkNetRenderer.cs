using System;
using System.Collections.Generic;
using System.Numerics;
using DirectUI.Core;
using DirectUI.Drawing;
using SkiaSharp;
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

        var typeface = _textService.GetOrCreateTypeface(style);
        using var font = new SKFont(typeface, style.FontSize);
        using var paint = new SKPaint(font)
        {
            Color = new SKColor(color.R, color.G, color.B, color.A),
            IsAntialias = true
        };

        var textBounds = new SKRect();
        paint.MeasureText(text, ref textBounds);
        var textDrawPos = origin;

        // --- Horizontal alignment (based on actual measured text width) ---
        if (maxSize.X > 0)
        {
            switch (alignment.Horizontal)
            {
                case HAlignment.Center:
                    textDrawPos.X += (maxSize.X - textBounds.Width) / 2f - textBounds.Left;
                    break;
                case HAlignment.Right:
                    textDrawPos.X += maxSize.X - textBounds.Width - textBounds.Left;
                    break;
                default: // Left
                    textDrawPos.X -= textBounds.Left;
                    break;
            }
        }
        else
        {
            textDrawPos.X -= textBounds.Left;
        }


        // --- Vertical alignment (based on stable font metrics to prevent jiggling) ---
        var fontMetrics = paint.FontMetrics;
        var baselineY = origin.Y;

        if (maxSize.Y > 0)
        {
            switch (alignment.Vertical)
            {
                case VAlignment.Top:
                    // Align the top of the text (ascent) with the top of the layout box.
                    // Since Ascent is negative, we subtract it.
                    baselineY -= fontMetrics.Ascent;
                    break;
                case VAlignment.Center:
                    // Center the line of text within the layout box.
                    float fontHeight = fontMetrics.Descent - fontMetrics.Ascent;
                    baselineY += (maxSize.Y - fontHeight) / 2f - fontMetrics.Ascent;

                    // Add a small correction for better visual centering, similar to the D2D backend.
                    // The metric center is often lower than the perceived visual center, so we move it up slightly.
                    baselineY -= 1.5f;
                    break;
                case VAlignment.Bottom:
                    // Align the bottom of the text (descent) with the bottom of the layout box.
                    baselineY += maxSize.Y - fontMetrics.Descent;
                    break;
            }
        }
        else
        {
            // If no max size, just align to top as a default
            baselineY -= fontMetrics.Ascent;
        }

        // The Y position for DrawText is the baseline, not the top.
        textDrawPos.Y = baselineY;


        _canvas.DrawText(text, textDrawPos.X, textDrawPos.Y, paint);
    }

    public void DrawImage(byte[] imageData, string imageKey, Rect destination)
    {
        if (_canvas is null || imageData is null || imageData.Length == 0) return;

        if (!_bitmapCache.TryGetValue(imageKey, out var bitmap))
        {
            try
            {
                bitmap = SKBitmap.Decode(imageData);
                if (bitmap != null)
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
        _canvas.DrawBitmap(bitmap, destRect);
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