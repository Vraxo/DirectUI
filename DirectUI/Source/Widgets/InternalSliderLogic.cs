using System;
using System.Numerics;
using Vortice.Mathematics;
using DirectUI.Core;

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
    protected bool isFocused = false;
    protected Vector2 trackPosition;
    protected float trackMinBound;
    protected float trackMaxBound;
    protected bool pendingTrackClickValueJump = false;
    protected float trackClickPosition = 0f;
    protected int GlobalIntId { get; private set; } = 0;


    // --- Calculated Properties ---
    public Rect GlobalBounds => new(Position.X - Origin.X, Position.Y - Origin.Y, Size.X, Size.Y);

    // --- Abstract Methods ---
    protected abstract void CalculateTrackBounds();
    protected abstract float HandleInput(InputState input, float currentValue);
    protected abstract float ConvertPositionToValue(float position);
    protected abstract Vector2 CalculateGrabberPosition(float currentValue);
    protected abstract void DrawForeground(IRenderer renderer, float currentValue);


    // --- Common Logic ---
    internal float UpdateAndDraw(int id, float currentValue)
    {
        var context = UI.Context;
        var state = UI.State;
        var renderer = context.Renderer;

        GlobalIntId = id;
        isFocused = state.FocusedElementId == GlobalIntId;
        trackPosition = Position - Origin;
        CalculateTrackBounds();

        pendingTrackClickValueJump = false;
        float newValue = currentValue;

        if (Disabled)
        {
            isGrabberHovered = false;
            isGrabberPressed = false;
            isTrackHovered = false;
            if (state.ActivelyPressedElementId == GlobalIntId) state.ClearActivePress(GlobalIntId);
        }
        else
        {
            newValue = HandleInput(context.InputState, currentValue);
        }

        if (pendingTrackClickValueJump && state.InputCaptorId == GlobalIntId && !state.NonSliderElementClaimedPress)
        {
            newValue = ConvertPositionToValue(trackClickPosition);
            newValue = ApplyStep(newValue);
            newValue = Math.Clamp(newValue, MinValue, MaxValue);
        }
        pendingTrackClickValueJump = false;

        UpdateGrabberThemeStyle();

        if (renderer is null)
        {
            Console.WriteLine("Error: Renderer is null during Slider UpdateAndDraw.");
            return newValue;
        }

        try
        {
            DrawBackground(renderer);
            DrawForeground(renderer, newValue);
            DrawGrabber(renderer, newValue);
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
        isGrabberPressed = UI.State.ActivelyPressedElementId == GlobalIntId;
        isFocused = UI.State.FocusedElementId == GlobalIntId;
        GrabberTheme.UpdateCurrentStyle(isGrabberHovered, isGrabberPressed, Disabled, isFocused);
    }


    // --- Drawing Methods ---
    protected void DrawBackground(IRenderer renderer)
    {
        // Explicitly pass X, Y, Width, Height components to Rect constructor
        renderer.DrawBox(new Rect(trackPosition.X, trackPosition.Y, Size.X, Size.Y), Theme.Background);
    }

    protected void DrawGrabber(IRenderer renderer, float currentValue)
    {
        Vector2 grabberPos = CalculateGrabberPosition(currentValue);
        // Explicitly pass X, Y, Width, Height components to Rect constructor
        renderer.DrawBox(new Rect(grabberPos.X, grabberPos.Y, GrabberSize.X, GrabberSize.Y), GrabberTheme.Current);
    }
}