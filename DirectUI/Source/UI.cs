// UI.cs
using System;
using System.Collections.Generic;
using System.Numerics;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;
using SharpGen.Runtime; // For ResultCode

namespace DirectUI;

public static class UI
{
    // --- Resource Cache ---
    private static readonly Dictionary<Color4, ID2D1SolidColorBrush> BrushCache = new();
    // TODO: Implement TextFormat caching

    // --- Public UI Methods ---

    /// <summary>
    /// Processes and draws a stateful Button.
    /// Renamed from Button to DoButton to avoid potential context errors.
    /// </summary>
    /// <param name="context">Drawing resources (RenderTarget, DWriteFactory).</param>
    /// <param name="input">Current input state.</param>
    /// <param name="button">The Button object to process and draw.</param>
    /// <returns>True if the button was clicked this frame according to its ActionMode.</returns>
    public static bool DoButton(DrawingContext context, InputState input, Button button) // Renamed here
    {
        if (button is null) return false;

        Rect bounds = button.GlobalBounds;
        bool wasClickedThisFrame = false;
        bool previousHoverState = button.IsHovering;

        // --- Update State based on Input ---
        if (button.Disabled)
        {
            button.IsHovering = false;
            button.IsPressed = false;
        }
        else
        {
            button.IsHovering = bounds.Contains(input.MousePosition.X, input.MousePosition.Y);

            if (button.Behavior == Button.ClickBehavior.Left || button.Behavior == Button.ClickBehavior.Both)
            {
                if (button.IsHovering && input.WasLeftMousePressedThisFrame)
                {
                    button.IsPressed = true;
                    if (button.LeftClickActionMode == Button.ActionMode.Press)
                    {
                        button.InvokeClick();
                        wasClickedThisFrame = true;
                    }
                }
                else if (button.IsPressed && !input.IsLeftMouseDown)
                {
                    if (button.IsHovering && button.LeftClickActionMode == Button.ActionMode.Release)
                    {
                        button.InvokeClick();
                        wasClickedThisFrame = true;
                    }
                    button.IsPressed = false;
                }
                else if (!input.IsLeftMouseDown)
                {
                    button.IsPressed = false;
                }
            }
        }

        // --- Trigger Enter/Exit Events ---
        if (button.IsHovering && !previousHoverState)
        {
            button.InvokeMouseEnter();
        }
        else if (!button.IsHovering && previousHoverState)
        {
            button.InvokeMouseExit();
        }

        // --- Update Style ---
        button.UpdateStyle();

        // --- Drawing ---
        try
        {
            DrawButtonBackground(context.RenderTarget, button);
            DrawButtonText(context, button);
        }
        catch (SharpGenException ex) when (ex.ResultCode.Code == ResultCode.RecreateTarget.Code)
        {
            Console.WriteLine($"Render target needs recreation (Caught SharpGenException during drawing): {ex.Message}");
            UI.CleanupResources();
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unhandled error during drawing: {ex}");
            UI.CleanupResources();
            return false;
        }


        return wasClickedThisFrame;
    }

    // --- Drawing Helpers ---

    private static void DrawButtonBackground(ID2D1RenderTarget renderTarget, Button button)
    {
        ButtonStyle style = button.Themes.Current;
        Rect bounds = button.GlobalBounds;

        ID2D1SolidColorBrush fillBrush = GetOrCreateBrush(renderTarget, style.FillColor);
        ID2D1SolidColorBrush borderBrush = GetOrCreateBrush(renderTarget, style.BorderColor);

        if (style.Roundness > 0.0f && bounds.Width > 0 && bounds.Height > 0)
        {
            float radiusX = bounds.Width * style.Roundness * 0.5f;
            float radiusY = bounds.Height * style.Roundness * 0.5f;
            var roundedRect = new Vortice.Direct2D1.RoundedRectangle((System.Drawing.RectangleF)bounds, radiusX, radiusY);
            if (style.FillColor.A > 0 && fillBrush is not null)
            {
                renderTarget.FillRoundedRectangle(roundedRect, fillBrush);
            }
            if (style.BorderThickness > 0 && style.BorderColor.A > 0 && borderBrush is not null)
            {
                renderTarget.DrawRoundedRectangle(roundedRect, borderBrush, style.BorderThickness);
            }
        }
        else
        {
            if (style.FillColor.A > 0 && fillBrush is not null)
            {
                renderTarget.FillRectangle(bounds, fillBrush);
            }
            if (style.BorderThickness > 0 && style.BorderColor.A > 0 && borderBrush is not null)
            {
                renderTarget.DrawRectangle(bounds, borderBrush, style.BorderThickness);
            }
        }
    }

    private static void DrawButtonText(DrawingContext context, Button button)
    {
        if (string.IsNullOrEmpty(button.Text)) return;

        ButtonStyle style = button.Themes.Current;
        ID2D1SolidColorBrush textBrush = GetOrCreateBrush(context.RenderTarget, style.FontColor);
        if (textBrush is null) return;

        IDWriteTextFormat? textFormat = null;
        try
        {
            textFormat = context.DWriteFactory.CreateTextFormat(
                style.FontName, null, style.FontWeight, style.FontStyle, style.FontStretch, style.FontSize, "en-us"
            );

            if (textFormat is null) throw new InvalidOperationException("Failed to create TextFormat.");

            textFormat.TextAlignment = button.TextAlignment.Horizontal switch
            {
                HAlignment.Left => TextAlignment.Leading,
                HAlignment.Center => TextAlignment.Center,
                HAlignment.Right => TextAlignment.Trailing,
                _ => TextAlignment.Leading
            };
            textFormat.ParagraphAlignment = button.TextAlignment.Vertical switch
            {
                VAlignment.Top => ParagraphAlignment.Near,
                VAlignment.Center => ParagraphAlignment.Center,
                VAlignment.Bottom => ParagraphAlignment.Far,
                _ => ParagraphAlignment.Near
            };

            Rect layoutRect = button.GlobalBounds;
            layoutRect.Left += button.TextOffset.X;
            layoutRect.Top += button.TextOffset.Y;

            context.RenderTarget.DrawText(button.Text, textFormat, layoutRect, textBrush);
        }
        finally
        {
            textFormat?.Dispose();
        }
    }

    // --- Resource Management Helpers ---

    private static ID2D1SolidColorBrush GetOrCreateBrush(ID2D1RenderTarget renderTarget, Color4 color)
    {
        if (BrushCache.TryGetValue(color, out ID2D1SolidColorBrush? brush))
        {
            if (brush is not null) return brush;
            else { BrushCache.Remove(color); }
        }

        try
        {
            brush = renderTarget.CreateSolidColorBrush(color);
            if (brush is not null)
            {
                BrushCache[color] = brush;
                return brush;
            }
            else
            {
                Console.WriteLine($"Warning: CreateSolidColorBrush returned null for color {color}");
                return null!;
            }
        }
        catch (SharpGenException ex) when (ex.ResultCode.Code == ResultCode.RecreateTarget.Code)
        {
            Console.WriteLine($"Brush creation failed due to RecreateTarget error (Color: {color}). Clearing cache.");
            CleanupResources();
            return null!;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating brush for color {color}: {ex.Message}");
            return null!;
        }
    }

    public static void CleanupResources()
    {
        Console.WriteLine("UI Resource Cleanup: Disposing cached brushes...");
        int count = BrushCache.Count;
        foreach (var pair in BrushCache)
        {
            pair.Value?.Dispose();
        }
        BrushCache.Clear();
        Console.WriteLine($"UI Resource Cleanup finished. Disposed {count} brushes.");
    }
}