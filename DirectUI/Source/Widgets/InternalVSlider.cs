// Summary: HandleInput now only returns a modified value if actively dragging. Otherwise returns currentValue. Sets pending flags as before.
using System;
using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Mathematics;
using D2D = Vortice.Direct2D1;

namespace DirectUI;

internal class InternalVSliderLogic : InternalSliderLogic
{
    public VSliderDirection Direction { get; set; } = VSliderDirection.TopToBottom;

    protected override void CalculateTrackBounds()
    {
        trackMinBound = trackPosition.Y;
        trackMaxBound = trackPosition.Y + Size.Y;
    }

    protected override void UpdateHoverStates(Vector2 mousePos) { }

    // Returns currentValue unless actively dragging this slider.
    // Sets pendingTrackClickValueJump if track is pressed.
    protected override float HandleInput(string id, InputState input, float currentValue)
    {
        Vector2 mousePos = input.MousePosition;

        // 1. Calculate Hover States & Set Potential Target
        Vector2 currentGrabberPos = CalculateGrabberPosition(currentValue);
        Rect currentGrabberBounds = new Rect(currentGrabberPos.X, currentGrabberPos.Y, GrabberSize.X, GrabberSize.Y);
        isGrabberHovered = currentGrabberBounds.Contains(mousePos.X, mousePos.Y);
        Rect trackBoundsRect = new Rect(trackPosition.X, trackPosition.Y, Size.X, Size.Y);
        isTrackHovered = trackBoundsRect.Contains(mousePos.X, mousePos.Y);
        bool isSliderHovered = isGrabberHovered || isTrackHovered;
        if (isSliderHovered)
        {
            UI.SetPotentialInputTarget(id);
        }

        // 2. Handle Mouse Release
        if (UI.ActivelyPressedElementId == id && !input.IsLeftMouseDown)
        {
            UI.ClearActivePress(id);
        }

        // 3. Handle Mouse Press Attempt
        if (input.WasLeftMousePressedThisFrame)
        {
            if (isSliderHovered && UI.PotentialInputTargetId == id && !UI.dragInProgressFromPreviousFrame)
            {
                UI.SetPotentialCaptorForFrame(id); // Attempt capture
                if (isTrackHovered && !isGrabberHovered)
                {
                    pendingTrackClickValueJump = true; // Mark pending jump
                    trackClickPosition = Math.Clamp(mousePos.Y, trackMinBound, trackMaxBound); // Store click pos
                    // DO NOT MODIFY value here - return currentValue below
                }
            }
        }

        // 4. Handle Mouse Held/Drag
        if (UI.ActivelyPressedElementId == id && input.IsLeftMouseDown)
        {
            // If dragging (and not just starting a deferred jump), calculate and return the dragged value.
            if (!pendingTrackClickValueJump)
            {
                float clampedY = Math.Clamp(mousePos.Y, trackMinBound, trackMaxBound);
                float draggedValue = ConvertPositionToValue(clampedY);
                draggedValue = ApplyStep(draggedValue);
                draggedValue = Math.Clamp(draggedValue, MinValue, MaxValue);
                return draggedValue; // Return the value modified by drag
            }
            // If pending jump, fall through to return original currentValue
        }

        // If not dragging, or pending jump, return the unmodified current value.
        // The jump, if it happens, will be handled later in UpdateAndDraw.
        return currentValue;
    }

    // --- Other methods unchanged ---
    protected override float ConvertPositionToValue(float position)
    {
        if (trackMaxBound <= trackMinBound) return MinValue;
        float normalized = (position - trackMinBound) / (trackMaxBound - trackMinBound);
        normalized = Math.Clamp(normalized, 0.0f, 1.0f);
        if (Direction == VSliderDirection.BottomToTop) { normalized = 1.0f - normalized; }
        float rawValue = MinValue + normalized * (MaxValue - MinValue);
        return rawValue;
    }
    protected override Vector2 CalculateGrabberPosition(float currentValue)
    {
        float valueRange = MaxValue - MinValue;
        float clampedValue = Math.Clamp(currentValue, MinValue, MaxValue);
        float normalizedValue = (valueRange > 0) ? (clampedValue - MinValue) / valueRange : 0;
        if (Direction == VSliderDirection.BottomToTop) { normalizedValue = 1.0f - normalizedValue; }
        float trackHeight = Size.Y;
        float centerY = trackMinBound + normalizedValue * trackHeight;
        float yPos = centerY - (GrabberSize.Y / 2.0f);
        float xPos = trackPosition.X + (Size.X / 2.0f) - (GrabberSize.X / 2.0f);
        float minY = trackPosition.Y;
        float maxY = trackPosition.Y + Size.Y - GrabberSize.Y;
        if (maxY < minY) maxY = minY;
        yPos = Math.Clamp(yPos, minY, maxY);
        return new Vector2(xPos, yPos);
    }
    protected override void DrawForeground(ID2D1RenderTarget renderTarget, float currentValue)
    {
        float valueRange = MaxValue - MinValue;
        if (valueRange <= 0 || renderTarget is null) return;
        float clampedValue = Math.Clamp(currentValue, MinValue, MaxValue);
        float normalizedValue = (valueRange > 0) ? (clampedValue - MinValue) / valueRange : 0.0f;
        float foregroundHeight = Size.Y * normalizedValue;
        if (foregroundHeight <= 0.001f) return;
        Rect clipRect;
        if (Direction == VSliderDirection.BottomToTop) { clipRect = new Rect(trackPosition.X, trackPosition.Y + Size.Y - foregroundHeight, Size.X, foregroundHeight); }
        else { clipRect = new Rect(trackPosition.X, trackPosition.Y, Size.X, foregroundHeight); }
        renderTarget.PushAxisAlignedClip(clipRect, D2D.AntialiasMode.Aliased);
        UI.DrawBoxStyleHelper(renderTarget, trackPosition, Size, Theme.Foreground);
        renderTarget.PopAxisAlignedClip();
    }
}