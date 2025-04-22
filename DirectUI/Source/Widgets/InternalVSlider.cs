// MODIFIED: DirectUI/InternalVSliderLogic.cs
// Summary: Full class code with restored methods and adjusted grabber position clamping.
using System;
using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Mathematics;
using D2D = Vortice.Direct2D1; // Alias

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
        // Assume not hovered initially, HandleInput refines this
        isGrabberHovered = false;
    }

    protected override float HandleInput(InputState input, float currentValue)
    {
        float newValue = currentValue;
        Vector2 mousePos = input.MousePosition;

        // Precise grabber check
        Vector2 currentGrabberPos = CalculateGrabberPosition(currentValue);
        Rect currentGrabberBounds = new Rect(currentGrabberPos.X, currentGrabberPos.Y, GrabberSize.X, GrabberSize.Y);
        // Use precise check for interaction
        bool preciseGrabberHovered = currentGrabberBounds.Contains(mousePos.X, mousePos.Y);
        // Update state field for theme
        isGrabberHovered = preciseGrabberHovered;

        if (input.WasLeftMousePressedThisFrame)
        {
            if (preciseGrabberHovered) // Use precise check for press
            {
                isGrabberPressed = true;
            }
            else if (isTrackHovered)
            {
                float clampedY = Math.Clamp(mousePos.Y, trackMinBound, trackMaxBound);
                newValue = ConvertPositionToValue(clampedY);
                newValue = ApplyStep(newValue);
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
            newValue = ApplyStep(newValue); // Apply step continuously during drag
        }

        // ToDo: Handle Mouse Wheel
        // ToDo: Handle Keyboard Navigation

        return Math.Clamp(newValue, MinValue, MaxValue);
    }

    protected override float ConvertPositionToValue(float position)
    {
        if (trackMaxBound <= trackMinBound) return MinValue;

        float normalized = (position - trackMinBound) / (trackMaxBound - trackMinBound);

        // Clamp normalized value before direction check
        normalized = Math.Clamp(normalized, 0.0f, 1.0f);

        if (Direction == VSliderDirection.BottomToTop)
        {
            normalized = 1.0f - normalized;
        }

        float rawValue = MinValue + normalized * (MaxValue - MinValue);
        return rawValue;
    }

    protected override Vector2 CalculateGrabberPosition(float currentValue)
    {
        float valueRange = MaxValue - MinValue;
        // Clamp value before calculating normalized
        float clampedValue = Math.Clamp(currentValue, MinValue, MaxValue);
        // Prevent division by zero
        float normalizedValue = (valueRange > 0) ? (clampedValue - MinValue) / valueRange : 0;

        if (Direction == VSliderDirection.BottomToTop)
        {
            normalizedValue = 1.0f - normalizedValue;
        }

        float trackHeight = Size.Y;
        // Center point calculation remains the same
        float centerY = trackMinBound + normalizedValue * trackHeight;

        // Calculate initial top-left position
        float yPos = centerY - (GrabberSize.Y / 2.0f);
        float xPos = trackPosition.X + (Size.X / 2.0f) - (GrabberSize.X / 2.0f);

        // --- ADJUSTED CLAMPING ---
        // Ensure the top edge of the grabber doesn't go past the top edge of the track.
        float minY = trackPosition.Y;
        // Ensure the bottom edge of the grabber doesn't go past the bottom edge of the track.
        float maxY = trackPosition.Y + Size.Y - GrabberSize.Y;

        // Clamp the calculated top-left position (yPos)
        // Ensure maxY is not less than minY if grabber is taller than track
        if (maxY < minY) maxY = minY;
        yPos = Math.Clamp(yPos, minY, maxY);
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

        float foregroundHeight = Size.Y * normalizedValue;

        // No need to draw if height is effectively zero
        if (foregroundHeight <= 0.001f) return;

        Rect clipRect;
        if (Direction == VSliderDirection.BottomToTop)
        {
            clipRect = new Rect(
                trackPosition.X,
                trackPosition.Y + Size.Y - foregroundHeight, // Start from bottom edge minus fill height
                Size.X,
                foregroundHeight
            );
        }
        else // TopToBottom
        {
            clipRect = new Rect(
                trackPosition.X,
                trackPosition.Y,
                Size.X,
                foregroundHeight
            );
        }

        // Push the clip
        renderTarget.PushAxisAlignedClip(clipRect, D2D.AntialiasMode.Aliased);

        // Draw the foreground BoxStyle using the *full track bounds*
        // This ensures the rounding radius is calculated based on the full Size.Y
        DrawBoxStyle(renderTarget, trackPosition, Size, Theme.Foreground);

        // Pop the clip
        renderTarget.PopAxisAlignedClip();
    }
}