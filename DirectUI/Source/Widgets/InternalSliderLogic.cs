// NEW: DirectUI/InternalSliderLogic.cs
// Summary: Base logic common to HSlider and VSlider internal representations.
using System;
using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Mathematics;

namespace DirectUI;

internal abstract class InternalSliderLogic
{
    // --- Configuration (Set externally via UI.HSlider/VSlider from SliderDefinition) ---
    public Vector2 Position { get; set; }
    public Vector2 Size { get; set; }
    public Vector2 Origin { get; set; }
    public float MinValue { get; set; }
    public float MaxValue { get; set; }
    public float Step { get; set; }
    public SliderStyle Theme { get; set; } = new(); // Default theme
    public ButtonStylePack GrabberTheme { get; set; } = new(); // Default theme
    public Vector2 GrabberSize { get; set; } = new(16, 16); // Default grabber size
    public bool Disabled { get; set; }
    public object? UserData { get; set; } // Not directly used by logic, but stored

    // --- Internal State ---
    protected bool isGrabberPressed = false;
    protected bool isGrabberHovered = false;
    protected bool isTrackHovered = false;
    protected Vector2 trackPosition; // Top-left of the track area
    protected float trackMinBound; // Min X or Y of the track bounds
    protected float trackMaxBound; // Max X or Y of the track bounds

    // --- Calculated Properties ---
    public Rect GlobalBounds => new(Position.X - Origin.X, Position.Y - Origin.Y, Size.X, Size.Y);

    // --- Abstract Methods (Implemented by HSlider/VSlider) ---
    protected abstract void CalculateTrackBounds();
    protected abstract void UpdateHoverStates(Vector2 mousePos);
    protected abstract float HandleInput(InputState input, float currentValue);
    protected abstract float ConvertPositionToValue(float position);
    protected abstract Vector2 CalculateGrabberPosition(float currentValue);
    protected abstract void DrawForeground(ID2D1RenderTarget renderTarget, float currentValue);

    // --- Common Logic ---
    internal float UpdateAndDraw(InputState input, DrawingContext context, float currentValue)
    {
        trackPosition = Position - Origin; // Calculate based on current Position/Origin
        CalculateTrackBounds(); // Calculate coordinate bounds

        float newValue = currentValue; // Start with the input value

        if (Disabled)
        {
            isGrabberHovered = false;
            isGrabberPressed = false;
            isTrackHovered = false;
        }
        else
        {
            UpdateHoverStates(input.MousePosition);
            newValue = HandleInput(input, currentValue); // Handle clicks, drags -> updates value
        }

        UpdateGrabberThemeStyle(); // Update grabber's Current style based on state

        // --- Drawing ---
        try
        {
            DrawBackground(context.RenderTarget);
            DrawForeground(context.RenderTarget, newValue); // Use the potentially updated value for drawing
            DrawGrabber(context.RenderTarget, newValue);
        }
        catch (Exception ex) // Catch drawing errors
        {
            Console.WriteLine($"Error drawing slider: {ex.Message}");
            // Don't crash, but the slider might not render correctly
        }


        return newValue; // Return the potentially modified value
    }

    protected float ApplyStep(float value)
    {
        float clampedValue = Math.Clamp(value, MinValue, MaxValue);
        if (Step <= 0 || MaxValue <= MinValue) // Avoid division by zero or invalid range
        {
            return clampedValue;
        }

        // Calculate how many steps fit into the range
        float range = MaxValue - MinValue;
        // Ensure steps can actually fit
        if (range < Step && Step > 0)
        {
            // If range is smaller than a step, snap to min or max
            return (value - MinValue < range / 2.0f) ? MinValue : MaxValue;
        }


        // Calculate the nearest step
        float stepsFromMin = (float)Math.Round((clampedValue - MinValue) / Step);
        float steppedValue = MinValue + stepsFromMin * Step;

        // Final clamp to ensure rounding didn't exceed bounds
        return Math.Clamp(steppedValue, MinValue, MaxValue);
    }


    protected void UpdateGrabberThemeStyle()
    {
        GrabberTheme.UpdateCurrentStyle(isGrabberHovered, isGrabberPressed, Disabled);
    }

    protected void DrawBackground(ID2D1RenderTarget renderTarget)
    {
        DrawBoxStyle(renderTarget, trackPosition, Size, Theme.Background);
    }

    protected void DrawGrabber(ID2D1RenderTarget renderTarget, float currentValue)
    {
        Vector2 grabberPos = CalculateGrabberPosition(currentValue);
        // Use the 'Current' style set by UpdateGrabberThemeStyle
        DrawBoxStyle(renderTarget, grabberPos, GrabberSize, GrabberTheme.Current);
    }

    // Helper to draw based on BoxStyle (Handles rounded vs sharp)
    protected static void DrawBoxStyle(ID2D1RenderTarget renderTarget, Vector2 pos, Vector2 size, BoxStyle style)
    {
        if (renderTarget is null || style is null || size.X <= 0 || size.Y <= 0)
        {
            return; // Nothing to draw
        }

        Rect bounds = new Rect(pos.X, pos.Y, size.X, size.Y);
        ID2D1SolidColorBrush fillBrush = UI.GetOrCreateBrush(style.FillColor);
        ID2D1SolidColorBrush borderBrush = UI.GetOrCreateBrush(style.BorderColor);

        bool canFill = style.FillColor.A > 0 && fillBrush is not null;
        bool canDrawBorder = style.BorderThickness > 0 && style.BorderColor.A > 0 && borderBrush is not null;

        if (style.Roundness > 0.0f)
        {
            var radiusX = Math.Max(0, bounds.Width * style.Roundness * 0.5f);
            var radiusY = Math.Max(0, bounds.Height * style.Roundness * 0.5f);
            // Vortice requires System.Drawing.RectangleF
            System.Drawing.RectangleF rectF = new(bounds.X, bounds.Y, bounds.Width, bounds.Height);
            RoundedRectangle roundedRect = new(rectF, radiusX, radiusY);

            if (canFill)
            {
                renderTarget.FillRoundedRectangle(roundedRect, fillBrush);
            }
            if (canDrawBorder)
            {
                renderTarget.DrawRoundedRectangle(roundedRect, borderBrush, style.BorderThickness);
            }
        }
        else
        {
            if (canFill)
            {
                renderTarget.FillRectangle(bounds, fillBrush);
            }
            if (canDrawBorder)
            {
                renderTarget.DrawRectangle(bounds, borderBrush, style.BorderThickness);
            }
        }
    }
}