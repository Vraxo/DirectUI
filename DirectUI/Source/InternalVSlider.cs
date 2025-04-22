// NEW: DirectUI/InternalVSliderLogic.cs
// Summary: Implements vertical-specific slider logic and drawing.
using System;
using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Mathematics;

namespace DirectUI;

internal class InternalVSliderLogic : InternalSliderLogic
{
    public VSliderDirection Direction { get; set; } = VSliderDirection.TopToBottom;

    protected override void CalculateTrackBounds()
    {
        trackMinBound = trackPosition.Y;
        trackMaxBound = trackPosition.Y + Size.Y;
    }

    protected override void UpdateHoverStates(Vector2 mousePos)
    {
        Rect trackBounds = new Rect(trackPosition.X, trackPosition.Y, Size.X, Size.Y);
        isTrackHovered = trackBounds.Contains(mousePos.X, mousePos.Y);

        // Simplified grabber hover check (precise check in HandleInput)
        isGrabberHovered = false;
        if (isTrackHovered)
        {
            Vector2 grabberPos = CalculateGrabberPosition(ApplyStep(MinValue + (MaxValue - MinValue) * 0.5f)); // Estimate midpoint
            Rect grabberBounds = new Rect(grabberPos.X, grabberPos.Y, GrabberSize.X, GrabberSize.Y);
            isGrabberHovered = grabberBounds.Contains(mousePos.X, mousePos.Y);
        }
    }

    protected override float HandleInput(InputState input, float currentValue)
    {
        float newValue = currentValue;
        Vector2 mousePos = input.MousePosition;

        // Precise grabber check
        Vector2 currentGrabberPos = CalculateGrabberPosition(currentValue);
        Rect currentGrabberBounds = new Rect(currentGrabberPos.X, currentGrabberPos.Y, GrabberSize.X, GrabberSize.Y);
        isGrabberHovered = currentGrabberBounds.Contains(mousePos.X, mousePos.Y);

        if (input.WasLeftMousePressedThisFrame)
        {
            if (isGrabberHovered)
            {
                isGrabberPressed = true;
            }
            else if (isTrackHovered)
            {
                float clampedY = Math.Clamp(mousePos.Y, trackMinBound, trackMaxBound);
                newValue = ConvertPositionToValue(clampedY);
                isGrabberPressed = true;
            }
        }
        else if (!input.IsLeftMouseDown)
        {
            isGrabberPressed = false;
        }

        if (isGrabberPressed)
        {
            float clampedY = Math.Clamp(mousePos.Y, trackMinBound, trackMaxBound);
            newValue = ConvertPositionToValue(clampedY);
        }

        // ToDo: Handle Mouse Wheel
        // ToDo: Handle Keyboard Navigation

        return ApplyStep(newValue);
    }

    protected override float ConvertPositionToValue(float position)
    {
        if (trackMaxBound <= trackMinBound) return MinValue;

        float normalized = (position - trackMinBound) / (trackMaxBound - trackMinBound);
        if (Direction == VSliderDirection.BottomToTop)
        {
            normalized = 1.0f - normalized;
        }

        float rawValue = normalized * (MaxValue - MinValue) + MinValue;
        return rawValue; // ApplyStep is done in HandleInput
    }

    protected override Vector2 CalculateGrabberPosition(float currentValue)
    {
        float valueRange = MaxValue - MinValue;
        float normalizedValue = (valueRange > 0) ? (currentValue - MinValue) / valueRange : 0;

        if (Direction == VSliderDirection.BottomToTop)
        {
            normalizedValue = 1.0f - normalizedValue;
        }

        float trackHeight = Size.Y;
        // Center the grabber visually on the value point
        float centerY = trackMinBound + normalizedValue * trackHeight;

        // Position grabber's top-left corner
        float yPos = centerY - (GrabberSize.Y / 2.0f);
        // Center horizontally within the track
        float xPos = trackPosition.X + (Size.X / 2.0f) - (GrabberSize.X / 2.0f);

        return new Vector2(xPos, yPos);
    }

    protected override void DrawForeground(ID2D1RenderTarget renderTarget, float currentValue)
    {
        float valueRange = MaxValue - MinValue;
        if (valueRange <= 0) return;

        float normalizedValue = (currentValue - MinValue) / valueRange;
        float foregroundHeight = Size.Y * normalizedValue;
        if (foregroundHeight <= 0) return;

        Vector2 foregroundPos = trackPosition;
        Vector2 foregroundSize = new Vector2(Size.X, foregroundHeight);

        if (Direction == VSliderDirection.BottomToTop)
        {
            foregroundPos.Y = trackPosition.Y + Size.Y - foregroundHeight;
        }

        DrawBoxStyle(renderTarget, foregroundPos, foregroundSize, Theme.Foreground);
    }
}