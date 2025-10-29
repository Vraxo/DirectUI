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
        scrollState.CalculatedContentSize = Vector2.Zero; // Reset for this frame's measurement pass.

        // Predict if a scrollbar will be needed based on the previous frame's content size.
        const float scrollbarThickness = 12f;
        const float scrollbarGap = 4f;
        bool scrollbarWillBeVisible = scrollState.ContentSize.Y > scrollState.VisibleSize.Y;
        availableInnerWidth = scrollbarWillBeVisible ? size.X - scrollbarThickness - scrollbarGap : size.X;

        // Handle scroll input
        if (scrollState.IsHovered && Context.InputState.ScrollDelta != 0)
        {
            var offset = scrollState.CurrentScrollOffset;
            offset.Y -= Context.InputState.ScrollDelta * 20; // Apply scroll wheel input
            scrollState.CurrentScrollOffset = offset;
        }

        // Clamp the offset *before* using it for layout, using content size from the *previous* frame.
        float maxScrollY = Math.Max(0, scrollState.ContentSize.Y - scrollState.VisibleSize.Y);
        var clampedOffset = scrollState.CurrentScrollOffset;
        clampedOffset.Y = Math.Clamp(clampedOffset.Y, 0, maxScrollY);
        scrollState.CurrentScrollOffset = clampedOffset;

        // Set the starting position for the content inside, offset by the scroll amount.
        var contentStartPosition = position - scrollState.CurrentScrollOffset;
        scrollState.SetContentStartPosition(contentStartPosition);

        Context.Layout.PushClipRect(regionBounds);
        Context.Renderer.PushClipRect(regionBounds, D2D.AntialiasMode.Aliased);

        Context.Layout.PushContainer(scrollState);
    }

    public static void EndScrollableRegion()
    {
        if (Context.Layout.ContainerStackCount == 0 || Context.Layout.PeekContainer() is not ScrollContainerState scrollState)
        { Console.WriteLine("Error: EndScrollableRegion called without a matching Begin."); return; }

        // The child container (e.g., a VBox) has called `Advance` on our state, so
        // `CalculatedContentSize` now holds the measured size of the content from this frame.
        // We store this for the *next* frame's layout prediction and clamping.
        scrollState.ContentSize = scrollState.CalculatedContentSize;

        // Pop the container and clip rects so the scrollbar can be drawn outside the content's clipped area.
        Context.Layout.PopContainer();
        Context.Renderer.PopClipRect();
        Context.Layout.PopClipRect();

        // Draw scrollbar if needed, based on this frame's measurements.
        if (scrollState.ContentSize.Y > scrollState.VisibleSize.Y)
        {
            string scrollBarIdString = scrollState.Id + "_scrollbar";
            float scrollbarThickness = 12f;
            var scrollBarPos = new Vector2(scrollState.Position.X + scrollState.VisibleSize.X - scrollbarThickness, scrollState.Position.Y);

            // The VScrollBar handles all its own logic and returns a new validated scroll offset.
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

        // Advance the main layout cursor by the size of the scroll region itself.
        Context.Layout.AdvanceLayout(scrollState.VisibleSize);
    }
}