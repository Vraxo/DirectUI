// NEW: DirectUI/InternalHSliderLogic.cs
// Summary: Implements horizontal-specific slider logic and drawing.
using System;
using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Mathematics;

namespace DirectUI;

internal class InternalHSliderLogic : InternalSliderLogic
{
    public HSliderDirection Direction { get; set; } = HSliderDirection.LeftToRight;

    protected override void CalculateTrackBounds()
    {
        trackMinBound = trackPosition.X;
        trackMaxBound = trackPosition.X + Size.X;
    }

    protected override void UpdateHoverStates(Vector2 mousePos)
    {
        Rect trackBounds = new Rect(trackPosition.X, trackPosition.Y, Size.X, Size.Y);
        isTrackHovered = trackBounds.Contains(mousePos.X, mousePos.Y);

        // Need current value to calculate grabber position for hover check
        // This is slightly problematic as hover state affects input, which affects value.
        // We'll use a temporary value or previous frame's value if available,
        // but for simplicity now, we might skip precise grabber hover before input.
        // Let's recalculate based on a hypothetical current value (passed in later?)
        // For now, assume track hover implies potential grabber hover.
        // A more robust solution might store the value used last frame.

        // Simplified: Check if mouse is near where grabber *would* be
        isGrabberHovered = false; // Requires value, calculate inside HandleInput or use rough check
        if (isTrackHovered)
        {
            // Precise check requires current value - skip for now or estimate
            Vector2 grabberPos = CalculateGrabberPosition(ApplyStep(MinValue + (MaxValue - MinValue) * 0.5f)); // Estimate at midpoint
            Rect grabberBounds = new Rect(grabberPos.X, grabberPos.Y, GrabberSize.X, GrabberSize.Y);
            isGrabberHovered = grabberBounds.Contains(mousePos.X, mousePos.Y);
        }
    }

    protected override float HandleInput(InputState input, float currentValue)
    {
        float newValue = currentValue;
        Vector2 mousePos = input.MousePosition;

        // Precise grabber check requires the current value
        Vector2 currentGrabberPos = CalculateGrabberPosition(currentValue);
        Rect currentGrabberBounds = new Rect(currentGrabberPos.X, currentGrabberPos.Y, GrabberSize.X, GrabberSize.Y);
        isGrabberHovered = currentGrabberBounds.Contains(mousePos.X, mousePos.Y); // Recalculate precisely

        if (input.WasLeftMousePressedThisFrame)
        {
            if (isGrabberHovered)
            {
                isGrabberPressed = true;
            }
            else if (isTrackHovered) // Clicked on track but not grabber
            {
                float clampedX = Math.Clamp(mousePos.X, trackMinBound, trackMaxBound);
                newValue = ConvertPositionToValue(clampedX);
                isGrabberPressed = true; // Start dragging immediately
            }
        }
        else if (!input.IsLeftMouseDown) // Released
        {
            isGrabberPressed = false;
        }


        if (isGrabberPressed) // Dragging
        {
            float clampedX = Math.Clamp(mousePos.X, trackMinBound, trackMaxBound);
            newValue = ConvertPositionToValue(clampedX);
        }

        // ToDo: Handle Mouse Wheel
        // if (isTrackHovered) { ... input.MouseWheelDelta ... }

        // ToDo: Handle Keyboard Navigation (if focus management is added)
        // if (HasFocus) { ... input.IsKeyPressed ... }

        return ApplyStep(newValue); // Ensure value adheres to step
    }

    protected override float ConvertPositionToValue(float position)
    {
        if (trackMaxBound <= trackMinBound) return MinValue; // Avoid division by zero

        float normalized = (position - trackMinBound) / (trackMaxBound - trackMinBound);
        if (Direction == HSliderDirection.RightToLeft)
        {
            normalized = 1.0f - normalized;
        }

        float rawValue = normalized * (MaxValue - MinValue) + MinValue;
        // ApplyStep will be called by HandleInput, no need to call it here directly
        return rawValue; // Return raw value, let HandleInput apply step
    }

    protected override Vector2 CalculateGrabberPosition(float currentValue)
    {
        float valueRange = MaxValue - MinValue;
        float normalizedValue = (valueRange > 0) ? (currentValue - MinValue) / valueRange : 0;

        if (Direction == HSliderDirection.RightToLeft)
        {
            normalizedValue = 1.0f - normalizedValue;
        }

        float trackWidth = Size.X;
        // Center the grabber visually on the value point
        float centerX = trackMinBound + normalizedValue * trackWidth;

        // Position grabber's top-left corner
        float xPos = centerX - (GrabberSize.X / 2.0f);
        // Center vertically within the track
        float yPos = trackPosition.Y + (Size.Y / 2.0f) - (GrabberSize.Y / 2.0f);

        return new Vector2(xPos, yPos);
    }

    protected override void DrawForeground(ID2D1RenderTarget renderTarget, float currentValue)
    {
        float valueRange = MaxValue - MinValue;
        if (valueRange <= 0) return; // Nothing to draw if range is zero or negative

        float normalizedValue = (currentValue - MinValue) / valueRange;
        float foregroundWidth = Size.X * normalizedValue;
        if (foregroundWidth <= 0) return; // Nothing to draw if width is zero or negative

        Vector2 foregroundPos = trackPosition;
        Vector2 foregroundSize = new Vector2(foregroundWidth, Size.Y);

        if (Direction == HSliderDirection.RightToLeft)
        {
            foregroundPos.X = trackPosition.X + Size.X - foregroundWidth;
        }

        DrawBoxStyle(renderTarget, foregroundPos, foregroundSize, Theme.Foreground);
    }
}