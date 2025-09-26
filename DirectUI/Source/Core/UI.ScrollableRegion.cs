using System;
using System.Numerics;
using Vortice.Mathematics;
using D2D = Vortice.Direct2D1;

namespace DirectUI;

public static partial class UI
{
    public static void BeginScrollableRegion(string id, Vector2 size, out float availableInnerWidth)
    {
        if (!IsContextValid())
        {
            availableInnerWidth = size.X;
            return;
        }

        int intId = id.GetHashCode();
        Vector2 position = Context.Layout.GetCurrentPosition();
        Rect regionBounds = new Rect(position.X, position.Y, size.X, size.Y);

        var scrollState = State.GetOrCreateElement<ScrollContainerState>(intId);
        scrollState.Id = intId;
        scrollState.Position = position;
        scrollState.VisibleSize = size;
        scrollState.IsHovered = regionBounds.Contains(Context.InputState.MousePosition);

        // Predict if a scrollbar will be needed based on the previous frame's content size.
        // This is a common and effective pattern in immediate-mode UIs.
        const float scrollbarThickness = 12f; // Must match the value in EndScrollableRegion
        const float scrollbarGap = 4f; // Add a small gap between content and scrollbar
        bool scrollbarWillBeVisible = scrollState.ContentSize.Y > scrollState.VisibleSize.Y;
        // If scrollbar is visible, reduce the available width by its thickness and a small gap.
        availableInnerWidth = scrollbarWillBeVisible ? size.X - scrollbarThickness - scrollbarGap : size.X;


        // Handle scroll input
        if (scrollState.IsHovered && Context.InputState.ScrollDelta != 0)
        {
            var offset = scrollState.CurrentScrollOffset;
            offset.Y -= Context.InputState.ScrollDelta * 20; // Apply scroll wheel input
            scrollState.CurrentScrollOffset = offset;
        }

        // Clamp the offset *before* using it for layout. Use the content size from the *previous* frame for this.
        float maxScrollY = Math.Max(0, scrollState.ContentSize.Y - scrollState.VisibleSize.Y);
        var clampedOffset = scrollState.CurrentScrollOffset;
        clampedOffset.Y = Math.Clamp(clampedOffset.Y, 0, maxScrollY);
        scrollState.CurrentScrollOffset = clampedOffset;

        // Begin the inner container for content layout, offset by the now-clamped scroll position
        var contentVBoxId = HashCode.Combine(intId, "scroll_vbox");
        var contentVBox = Context.Layout.GetOrCreateVBoxState(contentVBoxId);
        contentVBox.StartPosition = position - scrollState.CurrentScrollOffset;
        contentVBox.CurrentPosition = position - scrollState.CurrentScrollOffset;
        contentVBox.Gap = 0; // The user can nest another VBox inside for gaps
        contentVBox.MaxElementWidth = 0f;
        contentVBox.AccumulatedHeight = 0f;
        contentVBox.ElementCount = 0;
        scrollState.ContentVBox = contentVBox;

        Context.Layout.PushClipRect(regionBounds);
        Context.Renderer.PushClipRect(regionBounds, D2D.AntialiasMode.Aliased);

        Context.Layout.PushContainer(scrollState);
    }

    public static void EndScrollableRegion()
    {
        if (Context.Layout.ContainerStackCount == 0 || Context.Layout.PeekContainer() is not ScrollContainerState scrollState)
        { Console.WriteLine("Error: EndScrollableRegion called without a matching Begin."); return; }

        // Finalize content size based on what was rendered inside the container.
        // This new size will be used for clamping in the *next* frame.
        scrollState.ContentSize = new Vector2(scrollState.ContentVBox.MaxElementWidth, scrollState.ContentVBox.AccumulatedHeight);

        // Pop the container and clip rect so the scrollbar can be drawn outside the content's clipped area.
        Context.Layout.PopContainer();
        Context.Renderer.PopClipRect();
        Context.Layout.PopClipRect();

        // Draw scrollbar if needed. This will return a new, validated scroll offset.
        if (scrollState.ContentSize.Y > scrollState.VisibleSize.Y)
        {
            string scrollBarIdString = scrollState.Id + "_scrollbar";
            float scrollbarThickness = 12f;
            var scrollBarPos = new Vector2(scrollState.Position.X + scrollState.VisibleSize.X - scrollbarThickness, scrollState.Position.Y);

            // The VScrollBar handles all its own logic, including clamping the value against the new content size.
            float newScrollY = VScrollBar(
                id: scrollBarIdString,
                currentScrollOffset: scrollState.CurrentScrollOffset.Y,
                position: scrollBarPos,
                trackHeight: scrollState.VisibleSize.Y,
                contentHeight: scrollState.ContentSize.Y,
                visibleHeight: scrollState.VisibleSize.Y,
                thickness: scrollbarThickness);

            // Update the state with the value from the scrollbar for the next frame.
            scrollState.CurrentScrollOffset = new Vector2(scrollState.CurrentScrollOffset.X, newScrollY);
        }
        else
        {
            // If no scrollbar is needed, ensure the offset is zero.
            scrollState.CurrentScrollOffset = new Vector2(scrollState.CurrentScrollOffset.X, 0);
        }

        // After everything, advance the main layout cursor by the size of the scroll region itself.
        Context.Layout.AdvanceLayout(scrollState.VisibleSize);
    }
}