using System;
using System.Numerics;
using Vortice.Mathematics;
using D2D = Vortice.Direct2D1;

namespace DirectUI;

public static partial class UI
{
    public static void BeginScrollArea(string id, Vector2 size)
    {
        if (!IsContextValid())
        {
            return;
        }

        int intId = id.GetHashCode();
        var state = State.GetOrCreateElement<ScrollAreaState>(intId);
        var scale = Context.UIScale;

        // --- Step 1: Initialize per-frame state (all units are logical) ---
        state.Id = intId;
        state.Position = Context.Layout.GetCurrentPosition();
        state.VisibleSize = size;
        state.CalculatedContentSize = Vector2.Zero; // Reset for this frame's measurement.

        var viewRect = new Rect(state.Position.X * scale, state.Position.Y * scale, size.X * scale, size.Y * scale);
        state.IsHovered = viewRect.Contains(Context.InputState.MousePosition);

        // --- Step 2: Predict scrollbar visibility and calculate available space ---
        // This uses ContentSize from the PREVIOUS frame to avoid layout jitter.
        const float scrollbarThickness = 12f;
        bool vScrollWillBeVisible = state.ContentSize.Y > state.VisibleSize.Y;
        bool hScrollWillBeVisible = state.ContentSize.X > state.VisibleSize.X;

        float availableWidth = state.VisibleSize.X - (vScrollWillBeVisible ? scrollbarThickness : 0);
        float availableHeight = state.VisibleSize.Y - (hScrollWillBeVisible ? scrollbarThickness : 0);

        // --- Step 3: Handle scroll input and clamp offset ---
        // This also uses ContentSize from the PREVIOUS frame for clamping. Offset is LOGICAL.
        if (state.IsHovered)
        {
            var offset = state.CurrentScrollOffset;
            offset.Y -= Context.InputState.ScrollDelta * 40; // Apply vertical scroll wheel
            state.CurrentScrollOffset = offset;
        }

        float maxScrollX = Math.Max(0, state.ContentSize.X - availableWidth);
        float maxScrollY = Math.Max(0, state.ContentSize.Y - availableHeight);
        var clampedOffset = state.CurrentScrollOffset;
        clampedOffset.X = Math.Clamp(clampedOffset.X, 0, maxScrollX);
        clampedOffset.Y = Math.Clamp(clampedOffset.Y, 0, maxScrollY);
        state.CurrentScrollOffset = clampedOffset;

        // --- Step 4: Calculate content start position (with centering) ---
        float startX = state.Position.X;
        if (state.ContentSize.X < availableWidth) // Center horizontally if content is smaller
        {
            startX += (availableWidth - state.ContentSize.X) / 2f;
        }
        else // Otherwise, apply scroll offset
        {
            startX -= state.CurrentScrollOffset.X;
        }
        float startY = state.Position.Y - state.CurrentScrollOffset.Y;

        state.SetContentStartPosition(new Vector2(startX, startY));

        // --- Step 5: Push clipping and container state ---
        var contentClipRect = new Rect(state.Position.X * scale, state.Position.Y * scale, availableWidth * scale, availableHeight * scale);
        Context.Layout.PushClipRect(contentClipRect);
        Context.Renderer.PushClipRect(contentClipRect, D2D.AntialiasMode.Aliased);
        Context.Layout.PushContainer(state);
    }

    public static void EndScrollArea()
    {
        if (Context.Layout.ContainerStackCount == 0 || Context.Layout.PeekContainer() is not ScrollAreaState state)
        {
            Console.WriteLine("Error: EndScrollArea called without a matching BeginScrollArea.");
            return;
        }

        var scale = Context.UIScale;

        // --- Step 6: Pop container and clips ---
        Context.Layout.PopContainer();
        Context.Renderer.PopClipRect();
        Context.Layout.PopClipRect();

        // --- Step 7: Draw scrollbars based on THIS frame's content size ---
        // The child container has called Advance(), so state.CalculatedContentSize is now populated.
        var currentContentSize = state.CalculatedContentSize;
        const float scrollbarThickness = 12f;

        bool vScrollIsVisible = currentContentSize.Y > state.VisibleSize.Y;
        bool hScrollIsVisible = currentContentSize.X > state.VisibleSize.X;

        float availableWidth = state.VisibleSize.X - (vScrollIsVisible ? scrollbarThickness : 0);
        float availableHeight = state.VisibleSize.Y - (hScrollIsVisible ? scrollbarThickness : 0);

        var finalLogicalOffset = state.CurrentScrollOffset;

        if (vScrollIsVisible)
        {
            var scrollBarPos = new Vector2((state.Position.X + state.VisibleSize.X - scrollbarThickness) * scale, state.Position.Y * scale);
            float newPhysicalOffsetY = VScrollBar(
               id: state.Id + "_vscroll",
               currentScrollOffset: finalLogicalOffset.Y * scale,
               position: scrollBarPos,
               trackHeight: availableHeight * scale,
               contentHeight: currentContentSize.Y * scale,
               visibleHeight: availableHeight * scale,
               thickness: scrollbarThickness * scale);
            finalLogicalOffset.Y = newPhysicalOffsetY / scale;
        }
        else
        {
            finalLogicalOffset.Y = 0;
        }

        if (hScrollIsVisible)
        {
            var scrollBarPos = new Vector2(state.Position.X * scale, (state.Position.Y + state.VisibleSize.Y - scrollbarThickness) * scale);
            float newPhysicalOffsetX = HScrollBar(
               id: state.Id + "_hscroll",
               currentScrollOffset: finalLogicalOffset.X * scale,
               position: scrollBarPos,
               trackWidth: availableWidth * scale,
               contentWidth: currentContentSize.X * scale,
               visibleWidth: availableWidth * scale,
               thickness: scrollbarThickness * scale);
            finalLogicalOffset.X = newPhysicalOffsetX / scale;
        }
        else
        {
            finalLogicalOffset.X = 0;
        }

        // Update state with the final offset from scrollbar interactions for next frame.
        state.CurrentScrollOffset = finalLogicalOffset;

        // --- Step 8: Store this frame's content size for next frame's prediction ---
        state.ContentSize = currentContentSize;

        // --- Step 9: Advance parent layout ---
        Context.Layout.AdvanceLayout(state.VisibleSize);
    }
}