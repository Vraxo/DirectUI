using System.Numerics;
using Vortice.Mathematics;
using D2D = Vortice.Direct2D1;

namespace DirectUI;

public static partial class UI
{
    public static void BeginScrollableRegion(string id, Vector2 size, float gap = 0f, Vector2 padding = default)
    {
        if (!IsContextValid())
        {
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
        scrollState.CalculatedContentSize = Vector2.Zero;
        scrollState.Padding = padding;

        const float scrollbarThickness = 12f;
        bool scrollbarWillBeVisible = scrollState.ContentSize.Y > scrollState.VisibleSize.Y;

        float availableInnerWidth = size.X - (padding.X * 2) - (scrollbarWillBeVisible ? scrollbarThickness : 0);

        if (scrollState.IsHovered && Context.InputState.ScrollDelta != 0)
        {
            var offset = scrollState.CurrentScrollOffset;
            offset.Y -= Context.InputState.ScrollDelta * 20;
            scrollState.CurrentScrollOffset = offset;
        }

        float maxScrollY = Math.Max(0, scrollState.ContentSize.Y - scrollState.VisibleSize.Y);
        var clampedOffset = scrollState.CurrentScrollOffset;
        clampedOffset.Y = Math.Clamp(clampedOffset.Y, 0, maxScrollY);
        scrollState.CurrentScrollOffset = clampedOffset;

        var contentStartPosition = position - scrollState.CurrentScrollOffset;

        Context.Layout.PushClipRect(regionBounds);
        Context.Renderer.PushClipRect(regionBounds, D2D.AntialiasMode.Aliased);

        Context.ContentWidthStack.Push(availableInnerWidth);
        Context.Layout.PushContainer(scrollState);

        var vboxId = HashCode.Combine(intId, "internal_vbox");
        Context.Layout.BeginVBox(vboxId, contentStartPosition + padding, gap);

        var innerVBoxState = (VBoxContainerState)Context.Layout.PeekContainer();
        innerVBoxState.ConstrainedWidth = availableInnerWidth;
    }

    public static void EndScrollableRegion()
    {
        if (Context.Layout.ContainerStackCount < 2
            || Context.Layout.PeekContainer() is not VBoxContainerState innerVBox)
        { Console.WriteLine("Error: EndScrollableRegion found an invalid container stack. Mismatched Begin/End call."); return; }

        Context.Layout.PopContainer();

        if (Context.Layout.ContainerStackCount < 1
            || Context.Layout.PeekContainer() is not ScrollContainerState scrollState)
        { Console.WriteLine("Error: EndScrollableRegion could not find its own state on the stack."); return; }

        var innerContentSize = innerVBox.GetAccumulatedSize();
        var totalContentSize = innerContentSize + scrollState.Padding * 2;
        scrollState.ContentSize = totalContentSize;

        Context.ContentWidthStack.Pop();
        Context.Layout.PopContainer();
        Context.Renderer.PopClipRect();
        Context.Layout.PopClipRect();

        if (scrollState.ContentSize.Y > scrollState.VisibleSize.Y)
        {
            string scrollBarIdString = scrollState.Id + "_scrollbar";
            float scrollbarThickness = 12f;
            var scrollBarPos = new Vector2(scrollState.Position.X + scrollState.VisibleSize.X - scrollbarThickness, scrollState.Position.Y);

            float newScrollY = VScrollBar(
                id: scrollBarIdString,
                currentScrollOffset: scrollState.CurrentScrollOffset.Y,
                position: scrollBarPos,
                trackHeight: scrollState.VisibleSize.Y,
                contentHeight: scrollState.ContentSize.Y,
                visibleHeight: scrollState.VisibleSize.Y,
                thickness: scrollbarThickness);

            scrollState.CurrentScrollOffset = new Vector2(scrollState.CurrentScrollOffset.X, newScrollY);
        }
        else
        {
            scrollState.CurrentScrollOffset = new Vector2(scrollState.CurrentScrollOffset.X, 0);
        }

        Context.Layout.AdvanceLayout(scrollState.VisibleSize);
    }
}