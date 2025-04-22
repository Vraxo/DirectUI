// MODIFIED: DirectUI/InternalHSliderLogic.cs
// Summary: Full class code with restored methods and adjusted grabber position clamping.
using System;
using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Mathematics;
using D2D = Vortice.Direct2D1; // Alias

namespace DirectUI;

internal class InternalHSliderLogic : InternalSliderLogic
{
    public HSliderDirection Direction { get; set; } = HSliderDirection.LeftToRight;

    protected override void CalculateTrackBounds()
    {
        // trackPosition is set in base class UpdateAndDraw
        trackMinBound = trackPosition.X;
        trackMaxBound = trackPosition.X + Size.X;
    }

    protected override void UpdateHoverStates(Vector2 mousePos)
    {
        // Precise grabber hover depends on current value, calculated later in HandleInput.
        // We do a basic track hover check here.
        Rect trackBounds = new Rect(trackPosition.X, trackPosition.Y, Size.X, Size.Y);
        isTrackHovered = trackBounds.Contains(mousePos.X, mousePos.Y);
        // Assume grabber is not hovered initially; HandleInput will refine this.
        isGrabberHovered = false;
    }

    protected override float HandleInput(InputState input, float currentValue)
    {
        float newValue = currentValue;
        Vector2 mousePos = input.MousePosition;

        // Recalculate precise grabber hover state using the current value
        Vector2 currentGrabberPos = CalculateGrabberPosition(currentValue);
        Rect currentGrabberBounds = new Rect(currentGrabberPos.X, currentGrabberPos.Y, GrabberSize.X, GrabberSize.Y);
        // Use the precise check for interaction logic
        bool preciseGrabberHovered = currentGrabberBounds.Contains(mousePos.X, mousePos.Y);
        // Update the state field used for theme updates
        isGrabberHovered = preciseGrabberHovered;


        if (input.WasLeftMousePressedThisFrame)
        {
            if (preciseGrabberHovered) // Use precise check for press
            {
                isGrabberPressed = true;
            }
            else if (isTrackHovered) // Clicked on track but not grabber
            {
                float clampedX = Math.Clamp(mousePos.X, trackMinBound, trackMaxBound);
                newValue = ConvertPositionToValue(clampedX); // Get raw value from position
                newValue = ApplyStep(newValue); // Apply step based on the click position
                isGrabberPressed = true; // Start dragging immediately from the new value
            }
        }
        else if (!input.IsLeftMouseDown) // Released
        {
            isGrabberPressed = false;
        }


        if (isGrabberPressed) // Dragging
        {
            float clampedX = Math.Clamp(mousePos.X, trackMinBound, trackMaxBound);
            newValue = ConvertPositionToValue(clampedX); // Get raw value
                                                         // Apply step continuously during drag
            newValue = ApplyStep(newValue);
        }

        // ToDo: Handle Mouse Wheel
        // ToDo: Handle Keyboard Navigation

        // Final value is already stepped, just need to clamp (though ApplyStep should handle it)
        return Math.Clamp(newValue, MinValue, MaxValue);
    }

    protected override float ConvertPositionToValue(float position)
    {
        if (trackMaxBound <= trackMinBound) return MinValue; // Avoid division by zero

        float normalized = (position - trackMinBound) / (trackMaxBound - trackMinBound);

        // Clamp normalized value before direction check
        normalized = Math.Clamp(normalized, 0.0f, 1.0f);

        if (Direction == HSliderDirection.RightToLeft)
        {
            normalized = 1.0f - normalized;
        }

        float rawValue = MinValue + normalized * (MaxValue - MinValue);
        // ApplyStep is typically called *after* getting this raw value
        return rawValue;
    }

    protected override Vector2 CalculateGrabberPosition(float currentValue)
    {
        float valueRange = MaxValue - MinValue;
        // Clamp currentValue before calculating normalized value
        float clampedValue = Math.Clamp(currentValue, MinValue, MaxValue);
        // Prevent division by zero if range is zero
        float normalizedValue = (valueRange > 0) ? (clampedValue - MinValue) / valueRange : 0;

        if (Direction == HSliderDirection.RightToLeft)
        {
            normalizedValue = 1.0f - normalizedValue;
        }

        float trackWidth = Size.X;
        // Center point calculation remains the same
        float centerX = trackMinBound + normalizedValue * trackWidth;

        // Calculate initial top-left position
        float xPos = centerX - (GrabberSize.X / 2.0f);
        float yPos = trackPosition.Y + (Size.Y / 2.0f) - (GrabberSize.Y / 2.0f);

        // --- ADJUSTED CLAMPING ---
        // Ensure the left edge of the grabber doesn't go past the left edge of the track.
        float minX = trackPosition.X;
        // Ensure the right edge of the grabber doesn't go past the right edge of the track.
        float maxX = trackPosition.X + Size.X - GrabberSize.X;

        // Clamp the calculated top-left position (xPos)
        // Ensure maxX is not less than minX if grabber is wider than track
        if (maxX < minX) maxX = minX;
        xPos = Math.Clamp(xPos, minX, maxX);
        // --- END ADJUSTED CLAMPING ---

        return new Vector2(xPos, yPos);
    }

    // DrawForeground uses clipping (Corrected version from previous step)
    protected override void DrawForeground(ID2D1RenderTarget renderTarget, float currentValue)
    {
        float valueRange = MaxValue - MinValue;
        // Also check if render target is valid
        if (valueRange <= 0 || renderTarget is null) return;

        // Clamp value before calculating normalized value for drawing
        float clampedValue = Math.Clamp(currentValue, MinValue, MaxValue);
        float normalizedValue = (valueRange > 0) ? (clampedValue - MinValue) / valueRange : 0.0f;
        // No need to clamp normalizedValue again here as currentValue was clamped

        float foregroundWidth = Size.X * normalizedValue;

        // No need to draw if width is effectively zero
        if (foregroundWidth <= 0.001f) return;

        Rect clipRect;
        if (Direction == HSliderDirection.RightToLeft)
        {
            clipRect = new Rect(
                trackPosition.X + Size.X - foregroundWidth, // Start from the right edge minus fill width
                trackPosition.Y,
                foregroundWidth,
                Size.Y
            );
        }
        else // LeftToRight
        {
            clipRect = new Rect(
                trackPosition.X,
                trackPosition.Y,
                foregroundWidth,
                Size.Y
            );
        }

        // Push the clip
        // Use Aliased mode for sharp edges where the fill meets the background
        renderTarget.PushAxisAlignedClip(clipRect, D2D.AntialiasMode.Aliased);

        // Draw the foreground BoxStyle using the *full track bounds*
        // This ensures the rounding radius is calculated based on the full Size.X
        UI.DrawBoxStyleHelper(renderTarget, trackPosition, Size, Theme.Foreground);

        // Pop the clip
        renderTarget.PopAxisAlignedClip();
    }
}