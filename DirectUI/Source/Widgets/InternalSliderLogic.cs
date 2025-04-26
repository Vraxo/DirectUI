// Summary: Final version based on simplified logic. UpdateAndDraw checks InputCaptorId to perform deferred click. HandleInput sets flags/captor but only returns modified value on drag.
using System;
using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Mathematics;

namespace DirectUI;

internal abstract class InternalSliderLogic
{
    // --- Configuration ---
    public Vector2 Position { get; set; }
    public Vector2 Size { get; set; }
    public Vector2 Origin { get; set; }
    public float MinValue { get; set; }
    public float MaxValue { get; set; }
    public float Step { get; set; }
    public SliderStyle Theme { get; set; } = new();
    public ButtonStylePack GrabberTheme { get; set; } = new();
    public Vector2 GrabberSize { get; set; } = new(16, 16);
    public bool Disabled { get; set; }
    public object? UserData { get; set; }

    // --- Internal State ---
    protected bool isGrabberPressed = false; // Local visual/interaction state flag
    protected bool isGrabberHovered = false;
    protected bool isTrackHovered = false;
    protected Vector2 trackPosition;
    protected float trackMinBound;
    protected float trackMaxBound;
    protected bool pendingTrackClickValueJump = false; // Flag to defer track click action
    protected float trackClickPosition = 0f; // Position where track was clicked
    protected string GlobalId { get; private set; } = string.Empty; // Store ID for internal use

    // --- Calculated Properties ---
    public Rect GlobalBounds => new(Position.X - Origin.X, Position.Y - Origin.Y, Size.X, Size.Y);

    // --- Abstract Methods ---
    protected abstract void CalculateTrackBounds();
    protected abstract void UpdateHoverStates(Vector2 mousePos); // Potentially redundant, handled in HandleInput
    // HandleInput now ONLY returns a modified value if actively dragging.
    // Otherwise returns currentValue. Sets pendingTrackClickValueJump internally.
    protected abstract float HandleInput(string id, InputState input, float currentValue);
    protected abstract float ConvertPositionToValue(float position);
    protected abstract Vector2 CalculateGrabberPosition(float currentValue);
    protected abstract void DrawForeground(ID2D1RenderTarget renderTarget, float currentValue);


    // --- Common Logic ---
    // The main update and drawing method called by UI.HSlider/VSlider
    internal float UpdateAndDraw(string id, InputState input, DrawingContext context, float currentValue)
    {
        GlobalId = id; // Store the ID for internal checks
        trackPosition = Position - Origin;
        CalculateTrackBounds();

        pendingTrackClickValueJump = false; // Reset flag at the start of update
        float valueAfterInputHandling = currentValue; // Start with the input value

        if (Disabled)
        {
            // If disabled, reset states and ensure it's not marked as active
            isGrabberHovered = false;
            isGrabberPressed = false;
            isTrackHovered = false;
            if (UI.ActivelyPressedElementId == id) UI.ClearActivePress(id);
        }
        else
        {
            // HandleInput updates hover, potential target, potentially calls SetPotentialCaptorForFrame,
            // sets pendingTrackClickValueJump, and returns value potentially updated ONLY by active dragging.
            valueAfterInputHandling = HandleInput(id, input, currentValue);
        }

        // Determine the final value to use for drawing and returning.
        // Start with the value potentially modified by dragging (returned from HandleInput).
        float finalValue = valueAfterInputHandling;

        // --- Deferred Track Click Value Jump Check ---
        // Check if:
        // 1. A track click was initiated earlier in HandleInput (pending flag is true)
        // 2. This specific slider ended up being the final input captor for the frame
        if (pendingTrackClickValueJump && UI.InputCaptorId == id)
        {
            // Perform the value jump calculation NOW, overwriting the value
            finalValue = ConvertPositionToValue(trackClickPosition);
            finalValue = ApplyStep(finalValue);
            finalValue = Math.Clamp(finalValue, MinValue, MaxValue);
        }
        // Always reset flag after check, it's frame-specific
        pendingTrackClickValueJump = false;
        // --- End Deferred Check ---


        // Update visual style based on final state (hover, global active element, disabled)
        UpdateGrabberThemeStyle();

        if (context.RenderTarget is null)
        {
            Console.WriteLine("Error: RenderTarget is null during Slider UpdateAndDraw.");
            return finalValue; // Return the determined final value
        }

        // --- Drawing ---
        try
        {
            // Draw using the final determined value
            DrawBackground(context.RenderTarget);
            DrawForeground(context.RenderTarget, finalValue);
            DrawGrabber(context.RenderTarget, finalValue);
        }
        catch (SharpGen.Runtime.SharpGenException ex) when (ex.ResultCode.Code == Vortice.Direct2D1.ResultCode.RecreateTarget.Code)
        {
            Console.WriteLine($"Slider Draw failed (RecreateTarget): {ex.Message}. External cleanup needed.");
            // Consider requesting redraw or cleanup from the main application loop here
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error drawing slider '{id}': {ex.Message}");
        }

        // Return the final calculated value
        return finalValue;
    }

    // --- Helper Methods ---
    protected float ApplyStep(float value)
    {
        float clampedValue = Math.Clamp(value, MinValue, MaxValue);
        if (Step <= 0 || MaxValue <= MinValue)
        {
            return clampedValue; // No stepping possible or needed
        }

        float range = MaxValue - MinValue;
        // Handle case where range is smaller than step, snap to min/max
        if (range < Step && Step > 0)
        {
            return (value - MinValue < range / 2.0f) ? MinValue : MaxValue;
        }

        // Calculate the nearest step
        float stepsFromMin = (float)Math.Round((clampedValue - MinValue) / Step);
        float steppedValue = MinValue + stepsFromMin * Step;

        // Ensure the stepped value is still within bounds (due to potential floating point inaccuracies)
        return Math.Clamp(steppedValue, MinValue, MaxValue);
    }


    // Updates the grabber's visual style based on interaction state
    protected void UpdateGrabberThemeStyle()
    {
        // The grabber appears pressed if THIS slider is the globally active element.
        isGrabberPressed = UI.ActivelyPressedElementId == GlobalId;
        GrabberTheme.UpdateCurrentStyle(isGrabberHovered, isGrabberPressed, Disabled);
    }


    // --- Drawing Methods ---
    protected void DrawBackground(ID2D1RenderTarget renderTarget)
    {
        // Use the shared helper to draw the background track
        UI.DrawBoxStyleHelper(renderTarget, trackPosition, Size, Theme.Background);
    }

    protected void DrawGrabber(ID2D1RenderTarget renderTarget, float currentValue)
    {
        // Calculate the grabber's position based on the current value
        Vector2 grabberPos = CalculateGrabberPosition(currentValue);
        // Use the shared helper to draw the grabber using its current theme style
        UI.DrawBoxStyleHelper(renderTarget, grabberPos, GrabberSize, GrabberTheme.Current);
    }
}