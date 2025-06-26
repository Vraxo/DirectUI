using System;
using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Mathematics;

namespace DirectUI;

public static partial class UI
{
    // --- SHARED DRAWING HELPERS ---
    internal static void DrawBoxStyleHelper(ID2D1RenderTarget renderTarget, Vector2 pos, Vector2 size, BoxStyle style)
    {
        if (renderTarget is null || style is null || size.X <= 0 || size.Y <= 0) return;

        ID2D1SolidColorBrush fillBrush = GetOrCreateBrush(style.FillColor);
        ID2D1SolidColorBrush borderBrush = GetOrCreateBrush(style.BorderColor);

        float borderTop = Math.Max(0f, style.BorderLengthTop);
        float borderRight = Math.Max(0f, style.BorderLengthRight);
        float borderBottom = Math.Max(0f, style.BorderLengthBottom);
        float borderLeft = Math.Max(0f, style.BorderLengthLeft);

        bool hasVisibleFill = style.FillColor.A > 0 && fillBrush is not null;
        bool hasVisibleBorder = style.BorderColor.A > 0 && borderBrush is not null && (borderTop > 0 || borderRight > 0 || borderBottom > 0 || borderLeft > 0);

        if (!hasVisibleFill && !hasVisibleBorder) return;

        // --- Rounded Rectangle Rendering (Layered Approach) ---
        if (style.Roundness > 0.0f)
        {
            Rect outerBounds = new Rect(pos.X, pos.Y, size.X, size.Y);
            float maxRadius = Math.Min(outerBounds.Width * 0.5f, outerBounds.Height * 0.5f);
            float radius = Math.Max(0f, maxRadius * Math.Clamp(style.Roundness, 0.0f, 1.0f));

            if (float.IsFinite(radius) && radius >= 0)
            {
                // 1. Draw Border Area (Outer Rounded Rectangle)
                if (hasVisibleBorder)
                {
                    System.Drawing.RectangleF outerRectF = new(outerBounds.X, outerBounds.Y, outerBounds.Width, outerBounds.Height);
                    RoundedRectangle outerRoundedRect = new(outerRectF, radius, radius);
                    renderTarget.FillRoundedRectangle(outerRoundedRect, borderBrush);
                }

                // 2. Draw Fill Area (Inner Rounded Rectangle on top)
                if (hasVisibleFill)
                {
                    float fillX = pos.X + borderLeft;
                    float fillY = pos.Y + borderTop;
                    float fillWidth = Math.Max(0f, size.X - borderLeft - borderRight);
                    float fillHeight = Math.Max(0f, size.Y - borderTop - borderBottom);

                    if (fillWidth > 0 && fillHeight > 0)
                    {
                        // Adjust inner radius - clamp at zero. Use average border thickness for approximation.
                        float avgBorderX = (borderLeft + borderRight) * 0.5f;
                        float avgBorderY = (borderTop + borderBottom) * 0.5f;
                        float innerRadiusX = Math.Max(0f, radius - avgBorderX);
                        float innerRadiusY = Math.Max(0f, radius - avgBorderY);

                        System.Drawing.RectangleF fillRectF = new(fillX, fillY, fillWidth, fillHeight);
                        RoundedRectangle fillRoundedRect = new(fillRectF, innerRadiusX, innerRadiusY);
                        renderTarget.FillRoundedRectangle(fillRoundedRect, fillBrush);
                    }
                    // If fill consumes the whole area (e.g., no border), draw it directly using outer bounds/radius
                    else if (!hasVisibleBorder && fillBrush is not null)
                    {
                        System.Drawing.RectangleF outerRectF = new(outerBounds.X, outerBounds.Y, outerBounds.Width, outerBounds.Height);
                        RoundedRectangle outerRoundedRect = new(outerRectF, radius, radius);
                        renderTarget.FillRoundedRectangle(outerRoundedRect, fillBrush);
                    }
                }
                return; // Handled rounded case
            }
            // Fall through to sharp rendering if radius calculation failed
        }

        // --- Non-Rounded Rectangle Rendering (Layered Approach) ---

        // 1. Draw Border Area (Outer Rectangles)
        if (hasVisibleBorder && borderBrush is not null)
        {
            // Fill the entire background with border color first
            renderTarget.FillRectangle(new Rect(pos.X, pos.Y, size.X, size.Y), borderBrush);
        }

        // 2. Draw Fill Area (Inner Rectangle on top)
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
            // If fill consumes the whole area (e.g., no border), draw it directly
            else if (!hasVisibleBorder && fillBrush is not null)
            {
                renderTarget.FillRectangle(new Rect(pos.X, pos.Y, size.X, size.Y), fillBrush);
            }
        }
    }
}