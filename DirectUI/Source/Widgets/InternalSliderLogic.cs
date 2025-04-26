// Summary: UpdateAndDraw now checks the '!UI.nonSliderElementClaimedPress' flag before executing the deferred track click value jump. HandleInput now calls the general 'UI.SetPotentialCaptorForFrame'.
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
    protected bool isGrabberPressed = false;
    protected bool isGrabberHovered = false;
    protected bool isTrackHovered = false;
    protected Vector2 trackPosition;
    protected float trackMinBound;
    protected float trackMaxBound;
    protected bool pendingTrackClickValueJump = false;
    protected float trackClickPosition = 0f;
    protected string GlobalId { get; private set; } = string.Empty;

    // --- Calculated Properties ---
    public Rect GlobalBounds => new(Position.X - Origin.X, Position.Y - Origin.Y, Size.X, Size.Y);

    // --- Abstract Methods ---
    protected abstract void CalculateTrackBounds();
    protected abstract void UpdateHoverStates(Vector2 mousePos);
    protected abstract float HandleInput(string id, InputState input, float currentValue);
    protected abstract float ConvertPositionToValue(float position);
    protected abstract Vector2 CalculateGrabberPosition(float currentValue);
    protected abstract void DrawForeground(ID2D1RenderTarget renderTarget, float currentValue);


    // --- Common Logic ---
    internal float UpdateAndDraw(string id, InputState input, DrawingContext context, float currentValue)
    {
        GlobalId = id;
        trackPosition = Position - Origin;
        CalculateTrackBounds();

        pendingTrackClickValueJump = false;
        float newValue = currentValue;

        if (Disabled)
        {
            isGrabberHovered = false;
            isGrabberPressed = false;
            isTrackHovered = false;
            if (UI.ActivelyPressedElementId == id) UI.ClearActivePress(id);
        }
        else
        {
            // HandleInput sets potential target, calls SetPotentialCaptorForFrame,
            // sets pendingTrackClickValueJump, and returns value updated by DRAG.
            newValue = HandleInput(id, input, currentValue);
        }

        // Deferred Track Click Value Jump Check
        // Check if:
        // 1. A track click was initiated earlier (pending flag is true)
        // 2. This slider ended up being the final input captor for the frame
        // 3. A non-slider element (button) processed LATER did NOT claim the press
        if (pendingTrackClickValueJump && UI.InputCaptorId == id && !UI.nonSliderElementClaimedPress)
        {
            // Perform the value jump calculation now
            newValue = ConvertPositionToValue(trackClickPosition);
            newValue = ApplyStep(newValue);
            newValue = Math.Clamp(newValue, MinValue, MaxValue);
        }
        // Always reset flag after check
        pendingTrackClickValueJump = false;


        // Update visual style based on final state
        UpdateGrabberThemeStyle();

        if (context.RenderTarget is null)
        {
            Console.WriteLine("Error: RenderTarget is null during Slider UpdateAndDraw.");
            return newValue;
        }

        // --- Drawing ---
        try
        {
            DrawBackground(context.RenderTarget);
            DrawForeground(context.RenderTarget, newValue);
            DrawGrabber(context.RenderTarget, newValue);
        }
        catch (SharpGen.Runtime.SharpGenException ex) when (ex.ResultCode.Code == Vortice.Direct2D1.ResultCode.RecreateTarget.Code)
        {
            Console.WriteLine($"Slider Draw failed (RecreateTarget): {ex.Message}. External cleanup needed.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error drawing slider: {ex.Message}");
        }

        return newValue;
    }

    // --- Helper Methods ---
    protected float ApplyStep(float value)
    {
        float clampedValue = Math.Clamp(value, MinValue, MaxValue);
        if (Step <= 0 || MaxValue <= MinValue)
        {
            return clampedValue;
        }

        float range = MaxValue - MinValue;
        if (range < Step && Step > 0)
        {
            return (value - MinValue < range / 2.0f) ? MinValue : MaxValue;
        }

        float stepsFromMin = (float)Math.Round((clampedValue - MinValue) / Step);
        float steppedValue = MinValue + stepsFromMin * Step;

        return Math.Clamp(steppedValue, MinValue, MaxValue);
    }


    protected void UpdateGrabberThemeStyle()
    {
        isGrabberPressed = UI.ActivelyPressedElementId == GlobalId;
        GrabberTheme.UpdateCurrentStyle(isGrabberHovered, isGrabberPressed, Disabled);
    }


    // --- Drawing Methods ---
    protected void DrawBackground(ID2D1RenderTarget renderTarget)
    {
        UI.DrawBoxStyleHelper(renderTarget, trackPosition, Size, Theme.Background);
    }

    protected void DrawGrabber(ID2D1RenderTarget renderTarget, float currentValue)
    {
        Vector2 grabberPos = CalculateGrabberPosition(currentValue);
        UI.DrawBoxStyleHelper(renderTarget, grabberPos, GrabberSize, GrabberTheme.Current);
    }
}