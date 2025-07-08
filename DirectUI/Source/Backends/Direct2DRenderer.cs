// DirectUI/Backends/Direct2DRenderer.cs
using System;
using System.Collections.Generic;
using System.Numerics;
using DirectUI.Core;
using SharpGen.Runtime;
using Vortice.Direct2D1;
using Vortice.Mathematics;
using System.Drawing; // Added for RectangleF

namespace DirectUI.Backends;

/// <summary>
/// A rendering backend that uses Direct2D to implement the IRenderer interface.
/// It manages its own cache of Direct2D brushes.
/// </summary>
public class Direct2DRenderer : IRenderer
{
    private readonly ID2D1RenderTarget _renderTarget;
    private readonly Dictionary<Color4, ID2D1SolidColorBrush> _brushCache = new();

    public Vector2 RenderTargetSize => new(_renderTarget.Size.Width, _renderTarget.Size.Height);

    public Direct2DRenderer(ID2D1RenderTarget renderTarget)
    {
        _renderTarget = renderTarget ?? throw new ArgumentNullException(nameof(renderTarget));
    }

    public void DrawLine(Vector2 p1, Vector2 p2, Color4 color, float strokeWidth)
    {
        var brush = GetOrCreateBrush(color);
        if (brush is null) return;
        _renderTarget.DrawLine(p1, p2, brush, strokeWidth);
    }

    public void DrawBox(Rect rect, BoxStyle style)
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
            Rect outerBounds = new Rect(pos.X, pos.Y, size.X, size.Y);
            float maxRadius = Math.Min(outerBounds.Width * 0.5f, outerBounds.Height * 0.5f);
            float radius = Math.Max(0f, maxRadius * Math.Clamp(style.Roundness, 0.0f, 1.0f));

            if (float.IsFinite(radius) && radius >= 0)
            {
                if (hasVisibleBorder)
                {
                    // Convert Vortice.Mathematics.Rect to System.Drawing.RectangleF
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
                        // Convert Vortice.Mathematics.Rect to System.Drawing.RectangleF
                        System.Drawing.RectangleF fillRectF = new(fillX, fillY, fillWidth, fillHeight);
                        _renderTarget.FillRoundedRectangle(new RoundedRectangle(fillRectF, innerRadiusX, innerRadiusY), fillBrush);
                    }
                    else if (!hasVisibleBorder && fillBrush is not null)
                    {
                        // Convert Vortice.Mathematics.Rect to System.Drawing.RectangleF
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
                _renderTarget.FillRectangle(new Rect(fillX, fillY, fillWidth, fillHeight), fillBrush);
            }
            else if (!hasVisibleBorder && fillBrush is not null)
            {
                _renderTarget.FillRectangle(rect, fillBrush);
            }
        }
    }

    public void DrawTextLayout(Vector2 origin, ITextLayout textLayout, Color4 color)
    {
        if (textLayout is not DirectWriteTextLayout dwLayout) return;

        var brush = GetOrCreateBrush(color);
        if (brush is null) return;
        _renderTarget.DrawTextLayout(origin, dwLayout.DWriteLayout, brush, Vortice.Direct2D1.DrawTextOptions.None);
    }

    public void PushClipRect(Rect rect, AntialiasMode antialiasMode)
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
    }

    public ID2D1SolidColorBrush? GetOrCreateBrush(Color4 color)
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
            brush = _renderTarget.CreateSolidColorBrush(color);
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
            // Don't re-throw, allow graceful failure. The calling code should handle the null.
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating brush for color {color}: {ex.Message}");
            return null;
        }
    }
}