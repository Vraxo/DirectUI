// Widgets/InternalScrollBarLogic.cs
using System;
using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Mathematics;

namespace DirectUI;

/// <summary>
/// Internal class that encapsulates the state, logic, and drawing for a proportional scrollbar.
/// </summary>
internal class InternalScrollBarLogic
{
    // --- Configuration (set per-frame) ---
    public Vector2 Position { get; set; }
    public float TrackLength { get; set; }
    public float TrackThickness { get; set; }
    public bool IsVertical { get; set; }
    public float ContentSize { get; set; }
    public float VisibleSize { get; set; }
    public SliderStyle Theme { get; set; } = new();
    public ButtonStylePack ThumbTheme { get; set; } = new();

    // --- Internal State ---
    private int _id;
    private bool _isThumbHovered;
    private bool _isThumbPressed;
    private bool _isFocused;
    private float _dragStartMousePos;
    private float _dragStartScrollOffset;

    /// <summary>
    /// The main update and draw entry point called by the public UI method.
    /// </summary>
    public float UpdateAndDraw(int id, float currentScrollOffset)
    {
        if (VisibleSize >= ContentSize) return 0; // No scrolling needed, offset must be 0.

        _id = id;
        var context = UI.Context;
        var state = UI.State;
        var input = context.InputState;

        _isFocused = state.FocusedElementId == _id;
        float newScrollOffset = HandleInput(input, currentScrollOffset);

        _isThumbPressed = state.ActivelyPressedElementId == _id;
        ThumbTheme.UpdateCurrentStyle(_isThumbHovered, _isThumbPressed, false, _isFocused);

        if (context.RenderTarget != null)
        {
            Draw(context.RenderTarget, newScrollOffset);
        }

        return newScrollOffset;
    }

    private Rect GetTrackBounds()
    {
        return IsVertical
            ? new Rect(Position.X, Position.Y, TrackThickness, TrackLength)
            : new Rect(Position.X, Position.Y, TrackLength, TrackThickness);
    }

    private Rect GetThumbBounds(float currentScrollOffset, out float thumbLength)
    {
        thumbLength = 0;
        if (VisibleSize >= ContentSize) return new Rect();

        float viewRatio = VisibleSize / ContentSize;
        thumbLength = TrackLength * viewRatio;
        float minThumbLength = 20f; // Ensure thumb is always grabbable
        thumbLength = Math.Max(minThumbLength, thumbLength);

        float maxScrollOffset = ContentSize - VisibleSize;
        float scrollableTrackLength = TrackLength - thumbLength;

        if (scrollableTrackLength <= 0) return GetTrackBounds();

        float scrollRatio = (maxScrollOffset > 0) ? currentScrollOffset / maxScrollOffset : 0;
        float thumbOffsetInTrack = scrollableTrackLength * scrollRatio;

        return IsVertical
            ? new Rect(Position.X, Position.Y + thumbOffsetInTrack, TrackThickness, thumbLength)
            : new Rect(Position.X + thumbOffsetInTrack, Position.Y, thumbLength, TrackThickness);
    }

    private float HandleInput(InputState input, float currentScrollOffset)
    {
        var state = UI.State;
        var trackBounds = GetTrackBounds();
        var thumbBounds = GetThumbBounds(currentScrollOffset, out float thumbLength);

        _isThumbHovered = thumbBounds.Contains(input.MousePosition);
        bool isTrackHovered = trackBounds.Contains(input.MousePosition);

        if (_isThumbHovered || isTrackHovered)
        {
            state.SetPotentialInputTarget(_id);
        }

        _isThumbPressed = state.ActivelyPressedElementId == _id;

        if (_isThumbPressed && !input.IsLeftMouseDown)
        {
            state.ClearActivePress(_id);
            _isThumbPressed = false;
        }

        if (input.WasLeftMousePressedThisFrame && state.PotentialInputTargetId == _id)
        {
            state.SetButtonPotentialCaptorForFrame(_id);
            state.SetFocus(_id);
            _isThumbPressed = true;

            if (_isThumbHovered)
            {
                _dragStartMousePos = IsVertical ? input.MousePosition.Y : input.MousePosition.X;
                _dragStartScrollOffset = currentScrollOffset;
            }
            else // Clicked on track
            {
                float mousePosOnTrack = IsVertical ? input.MousePosition.Y : input.MousePosition.X;
                float thumbStart = IsVertical ? thumbBounds.Y : thumbBounds.X;
                float thumbEnd = thumbStart + thumbLength;

                float pageAmount = VisibleSize;
                if (mousePosOnTrack < thumbStart)
                {
                    return Math.Max(0, currentScrollOffset - pageAmount);
                }
                else if (mousePosOnTrack > thumbEnd)
                {
                    float maxScroll = ContentSize - VisibleSize;
                    return Math.Min(maxScroll, currentScrollOffset + pageAmount);
                }
            }
        }

        if (_isThumbPressed && input.IsLeftMouseDown)
        {
            float maxScrollOffset = ContentSize - VisibleSize;
            float scrollableTrackLength = TrackLength - thumbLength;
            if (scrollableTrackLength <= 0) return currentScrollOffset;

            float currentMousePos = IsVertical ? input.MousePosition.Y : input.MousePosition.X;
            float mouseDelta = currentMousePos - _dragStartMousePos;

            float scrollDelta = mouseDelta * (maxScrollOffset / scrollableTrackLength);

            float newScrollOffset = _dragStartScrollOffset + scrollDelta;
            return Math.Clamp(newScrollOffset, 0, maxScrollOffset);
        }

        return currentScrollOffset;
    }

    private void Draw(ID2D1RenderTarget rt, float currentScrollOffset)
    {
        // Draw Track
        UI.Resources.DrawBoxStyleHelper(rt, Position,
            IsVertical ? new Vector2(TrackThickness, TrackLength) : new Vector2(TrackLength, TrackThickness),
            Theme.Background);

        // Draw Thumb
        var thumbBounds = GetThumbBounds(currentScrollOffset, out _);
        if (thumbBounds.Width > 0 && thumbBounds.Height > 0)
        {
            var thumbStyle = new BoxStyle
            {
                FillColor = ThumbTheme.Current.FillColor,
                BorderColor = ThumbTheme.Current.BorderColor,
                BorderLength = ThumbTheme.Current.BorderLength,
                Roundness = ThumbTheme.Current.Roundness
            };
            // BUG FIX: Convert Vortice.Mathematics.Rect.Size to a System.Numerics.Vector2
            UI.Resources.DrawBoxStyleHelper(rt, thumbBounds.TopLeft, new Vector2(thumbBounds.Width, thumbBounds.Height), thumbStyle);
        }
    }
}