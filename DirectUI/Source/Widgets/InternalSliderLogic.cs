// MODIFIED: DirectUI/InternalSliderLogic.cs
// Summary: Full class. Removed local DrawBoxStyle/DrawSharpRectangle helpers. Updated DrawBackground/DrawGrabber to call UI.DrawBoxStyleHelper. Includes previous fixes for grabber position clamping.
using System;
using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Mathematics;
// Removed D2D Alias as it's no longer needed directly in this file for drawing helpers

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
        // Calculate current bounds based on potentially updated Position/Origin/Size
        trackPosition = Position - Origin;
        CalculateTrackBounds(); // Calculate coordinate bounds (min/max)

        float newValue = currentValue; // Start with the input value

        if (Disabled)
        {
            // Reset interaction states if disabled
            isGrabberHovered = false;
            isGrabberPressed = false;
            isTrackHovered = false;
        }
        else
        {
            // Update hover states first (this might be refined in HandleInput)
            UpdateHoverStates(input.MousePosition);
            // Handle input, which might change the value and refine hover/press states
            newValue = HandleInput(input, currentValue);
        }

        // Update grabber's visual style based on potentially new state
        UpdateGrabberThemeStyle();

        // --- Drawing ---
        // Ensure render target is valid before drawing
        if (context.RenderTarget is null)
        {
            Console.WriteLine("Error: RenderTarget is null during Slider UpdateAndDraw.");
            return newValue; // Cannot draw
        }

        try
        {
            // Draw track background
            DrawBackground(context.RenderTarget);
            // Draw filled portion of the track (foreground) using the updated value
            DrawForeground(context.RenderTarget, newValue);
            // Draw the grabber using the updated value
            DrawGrabber(context.RenderTarget, newValue);
        }
        catch (SharpGen.Runtime.SharpGenException ex) when (ex.ResultCode.Code == Vortice.Direct2D1.ResultCode.RecreateTarget.Code)
        {
            // Handle cases where drawing operations fail due to lost device
            Console.WriteLine($"Slider Draw failed (RecreateTarget): {ex.Message}. External cleanup needed.");
            // Don't throw, allow frame to potentially finish, rely on external handling
        }
        catch (Exception ex) // Catch other drawing errors
        {
            Console.WriteLine($"Error drawing slider: {ex.Message}");
            // Don't crash, but the slider might not render correctly
        }

        return newValue; // Return the potentially modified value
    }

    protected float ApplyStep(float value)
    {
        float clampedValue = Math.Clamp(value, MinValue, MaxValue);
        // Prevent issues if step is invalid or range is zero
        if (Step <= 0 || MaxValue <= MinValue)
        {
            return clampedValue;
        }

        float range = MaxValue - MinValue;
        // Handle case where range is smaller than a single step
        if (range < Step && Step > 0)
        {
            // Snap to the closer end
            return (value - MinValue < range / 2.0f) ? MinValue : MaxValue;
        }

        // Calculate the nearest step multiple from the minimum value
        float stepsFromMin = (float)Math.Round((clampedValue - MinValue) / Step);
        float steppedValue = MinValue + stepsFromMin * Step;

        // Final clamp to ensure rounding didn't exceed bounds (important due to float precision)
        return Math.Clamp(steppedValue, MinValue, MaxValue);
    }


    protected void UpdateGrabberThemeStyle()
    {
        // Use the state fields updated during HandleInput/UpdateHoverStates
        GrabberTheme.UpdateCurrentStyle(isGrabberHovered, isGrabberPressed, Disabled);
    }

    // --- MODIFIED DRAWING CALLS ---
    protected void DrawBackground(ID2D1RenderTarget renderTarget)
    {
        // Call the helper method now located in UI class
        UI.DrawBoxStyleHelper(renderTarget, trackPosition, Size, Theme.Background);
    }

    protected void DrawGrabber(ID2D1RenderTarget renderTarget, float currentValue)
    {
        Vector2 grabberPos = CalculateGrabberPosition(currentValue);
        // Call the helper method now located in UI class
        // Use the 'Current' style which was set by UpdateGrabberThemeStyle
        UI.DrawBoxStyleHelper(renderTarget, grabberPos, GrabberSize, GrabberTheme.Current);
    }

    // --- REMOVED DRAWING HELPERS (Moved to UI.cs) ---
}