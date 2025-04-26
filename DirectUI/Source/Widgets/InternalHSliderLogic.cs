// Summary: Changed the press condition to check '!UI.dragInProgressFromPreviousFrame'.
using System;
using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Mathematics;
using D2D = Vortice.Direct2D1;

namespace DirectUI;

internal class InternalHSliderLogic : InternalSliderLogic
{
    public HSliderDirection Direction { get; set; } = HSliderDirection.LeftToRight;

    protected override void CalculateTrackBounds()
    {
        trackMinBound = trackPosition.X;
        trackMaxBound = trackPosition.X + Size.X;
    }

    protected override void UpdateHoverStates(Vector2 mousePos) { }

    protected override float HandleInput(string id, InputState input, float currentValue)
    {
        float newValue = currentValue;
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
            isGrabberPressed = false;
            UI.ClearActivePress(id);
        }

        // 3. Handle Mouse Press Attempt
        if (input.WasLeftMousePressedThisFrame)
        {
            // Check if slider is hovered, is potential target, AND no drag was already happening.
            if (isSliderHovered && UI.PotentialInputTargetId == id && !UI.dragInProgressFromPreviousFrame)
            {
                UI.SetPotentialCaptorForFrame(id);
                isGrabberPressed = true; // Local state activation

                // Jump value if click was on track (not grabber)
                if (isTrackHovered && !isGrabberHovered)
                {
                    float clampedX = Math.Clamp(mousePos.X, trackMinBound, trackMaxBound);
                    newValue = ConvertPositionToValue(clampedX);
                    newValue = ApplyStep(newValue);
                }
            }
        }

        // 4. Handle Mouse Held/Drag (Checks current active element, which is correct)
        if (UI.ActivelyPressedElementId == id && input.IsLeftMouseDown)
        {
            isGrabberPressed = true;

            float clampedX = Math.Clamp(mousePos.X, trackMinBound, trackMaxBound);
            newValue = ConvertPositionToValue(clampedX);
            newValue = ApplyStep(newValue);
        }
        // If mouse is up OR this slider is NOT the active element anymore, ensure local press state is false
        else if (!input.IsLeftMouseDown || UI.ActivelyPressedElementId != id)
        {
            isGrabberPressed = false;
        }


        return Math.Clamp(newValue, MinValue, MaxValue);
    }

    // --- Other methods unchanged ---
    protected override float ConvertPositionToValue(float position)
    {
        if (trackMaxBound <= trackMinBound) return MinValue;

        float normalized = (position - trackMinBound) / (trackMaxBound - trackMinBound);
        normalized = Math.Clamp(normalized, 0.0f, 1.0f);

        if (Direction == HSliderDirection.RightToLeft)
        {
            normalized = 1.0f - normalized;
        }

        float rawValue = MinValue + normalized * (MaxValue - MinValue);
        return rawValue;
    }

    protected override Vector2 CalculateGrabberPosition(float currentValue)
    {
        float valueRange = MaxValue - MinValue;
        float clampedValue = Math.Clamp(currentValue, MinValue, MaxValue);
        float normalizedValue = (valueRange > 0) ? (clampedValue - MinValue) / valueRange : 0;

        if (Direction == HSliderDirection.RightToLeft)
        {
            normalizedValue = 1.0f - normalizedValue;
        }

        float trackWidth = Size.X;
        float centerX = trackMinBound + normalizedValue * trackWidth;

        float xPos = centerX - (GrabberSize.X / 2.0f);
        float yPos = trackPosition.Y + (Size.Y / 2.0f) - (GrabberSize.Y / 2.0f);

        float minX = trackPosition.X;
        float maxX = trackPosition.X + Size.X - GrabberSize.X;
        if (maxX < minX) maxX = minX;
        xPos = Math.Clamp(xPos, minX, maxX);

        return new Vector2(xPos, yPos);
    }

    protected override void DrawForeground(ID2D1RenderTarget renderTarget, float currentValue)
    {
        float valueRange = MaxValue - MinValue;
        if (valueRange <= 0 || renderTarget is null) return;

        float clampedValue = Math.Clamp(currentValue, MinValue, MaxValue);
        float normalizedValue = (valueRange > 0) ? (clampedValue - MinValue) / valueRange : 0.0f;
        float foregroundWidth = Size.X * normalizedValue;

        if (foregroundWidth <= 0.001f) return;

        Rect clipRect;
        if (Direction == HSliderDirection.RightToLeft)
        {
            clipRect = new Rect(trackPosition.X + Size.X - foregroundWidth, trackPosition.Y, foregroundWidth, Size.Y);
        }
        else // LeftToRight
        {
            clipRect = new Rect(trackPosition.X, trackPosition.Y, foregroundWidth, Size.Y);
        }

        renderTarget.PushAxisAlignedClip(clipRect, D2D.AntialiasMode.Aliased);
        UI.DrawBoxStyleHelper(renderTarget, trackPosition, Size, Theme.Foreground);
        renderTarget.PopAxisAlignedClip();
    }
}