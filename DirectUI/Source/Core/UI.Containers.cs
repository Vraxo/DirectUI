// Core/UI.Containers.cs
using System;
using System.Numerics;
using Vortice.Mathematics;
using D2D = Vortice.Direct2D1;

namespace DirectUI;

public static partial class UI
{
    public static void BeginHBoxContainer(string id, Vector2 position, float gap = 5.0f)
    {
        Context.Layout.BeginHBox(id.GetHashCode(), position, gap);
    }

    public static void EndHBoxContainer()
    {
        if (Context.Layout.ContainerStackCount == 0 || Context.Layout.PeekContainer() is not HBoxContainerState state)
        { Console.WriteLine("Error: EndHBoxContainer called without a matching BeginHBoxContainer."); return; }
        Context.Layout.PopContainer();
        if (Context.Layout.IsInLayoutContainer())
        { Context.Layout.AdvanceContainerLayout(new Vector2(state.AccumulatedWidth, state.MaxElementHeight)); }
    }

    public static void BeginVBoxContainer(string id, Vector2 position, float gap = 5.0f)
    {
        Context.Layout.BeginVBox(id.GetHashCode(), position, gap);
    }

    public static void EndVBoxContainer()
    {
        if (Context.Layout.ContainerStackCount == 0 || Context.Layout.PeekContainer() is not VBoxContainerState state)
        { Console.WriteLine("Error: EndVBoxContainer called without a matching BeginVBoxContainer."); return; }
        Context.Layout.PopContainer();
        if (Context.Layout.IsInLayoutContainer())
        { Context.Layout.AdvanceContainerLayout(new Vector2(state.MaxElementWidth, state.AccumulatedHeight)); }
    }

    public static void BeginGridContainer(string id, Vector2 position, Vector2 availableSize, int numColumns, Vector2 gap)
    {
        Context.Layout.PushContainer(new GridContainerState(id.GetHashCode(), position, availableSize, numColumns, gap));
    }

    public static void EndGridContainer()
    {
        if (Context.Layout.ContainerStackCount == 0 || Context.Layout.PeekContainer() is not GridContainerState state)
        { Console.WriteLine("Error: EndGridContainer called without a matching BeginGridContainer."); return; }
        Context.Layout.PopContainer();
        if (Context.Layout.IsInLayoutContainer())
        {
            Vector2 containerSize = state.GetTotalOccupiedSize();
            Context.Layout.AdvanceContainerLayout(containerSize);
        }
    }

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

    public static void BeginResizableVPanel(
        string id,
        ref float currentWidth,
        HAlignment alignment = HAlignment.Left,
        float topOffset = 0f,
        float minWidth = 50f,
        float maxWidth = 500f,
        float resizeHandleWidth = 5f,
        BoxStyle? panelStyle = null,
        Vector2 padding = default,
        float gap = 5f,
        bool disabled = false)
    {
        if (!IsContextValid()) return;
        var intId = id.GetHashCode();

        Vector2 finalPadding = (padding == default) ? new Vector2(5, 5) : padding;

        var input = Context.InputState;
        var renderer = Context.Renderer;
        var windowWidth = renderer.RenderTargetSize.X;
        var windowHeight = renderer.RenderTargetSize.Y;
        var availableHeight = windowHeight - topOffset;

        if (!disabled)
        {
            float handleWidth = Math.Min(resizeHandleWidth, currentWidth);
            float panelX = (alignment == HAlignment.Right) ? windowWidth - currentWidth : 0;
            float handleX = (alignment == HAlignment.Right) ? panelX : panelX + currentWidth - handleWidth;
            Rect handleRect = new Rect(handleX, topOffset, handleWidth, availableHeight);

            bool isHoveringHandle = handleRect.Contains(input.MousePosition.X, input.MousePosition.Y);
            if (isHoveringHandle) State.SetPotentialInputTarget(intId);

            if (input.WasLeftMousePressedThisFrame && isHoveringHandle && State.PotentialInputTargetId == intId && !State.DragInProgressFromPreviousFrame)
            {
                State.TrySetActivePress(intId, 10);
            }

            if (State.ActivelyPressedElementId == intId && !input.IsLeftMouseDown) State.ClearActivePress(intId);

            if (State.ActivelyPressedElementId == intId && input.IsLeftMouseDown)
            {
                if (alignment == HAlignment.Left) currentWidth = Math.Clamp(input.MousePosition.X, minWidth, maxWidth);
                else currentWidth = Math.Clamp(windowWidth - input.MousePosition.X, minWidth, maxWidth);
            }
        }

        var finalPanelStyle = panelStyle ?? new BoxStyle { FillColor = new(0.15f, 0.15f, 0.2f, 1.0f), BorderColor = DefaultTheme.NormalBorder, BorderLength = 1 };
        currentWidth = Math.Max(0, currentWidth);
        float finalPanelX = (alignment == HAlignment.Right) ? windowWidth - currentWidth : 0;
        Rect panelRect = new Rect(finalPanelX, topOffset, currentWidth, availableHeight);
        if (panelRect.Width > 0 && panelRect.Height > 0)
        {
            renderer.DrawBox(new Rect(panelRect.X, panelRect.Y, panelRect.Width, panelRect.Height), finalPanelStyle);
        }

        Vector2 contentStartPosition = new Vector2(finalPanelX + finalPadding.X, topOffset + finalPadding.Y);
        Rect contentClipRect = new Rect(contentStartPosition.X, contentStartPosition.Y, Math.Max(0, currentWidth - (finalPadding.X * 2)), Math.Max(0, availableHeight - (finalPadding.Y * 2)));
        bool clipPushed = false;
        if (contentClipRect.Width > 0 && contentClipRect.Height > 0)
        {
            renderer.PushClipRect(contentClipRect, D2D.AntialiasMode.Aliased);
            Context.Layout.PushClipRect(contentClipRect); // For culling
            clipPushed = true;
        }

        var vboxId = HashCode.Combine(intId, "_vbox");
        var vboxState = Context.Layout.GetOrCreateVBoxState(vboxId);
        vboxState.StartPosition = contentStartPosition;
        vboxState.CurrentPosition = contentStartPosition;
        vboxState.Gap = gap;
        vboxState.MaxElementWidth = 0f;
        vboxState.AccumulatedHeight = 0f;
        vboxState.ElementCount = 0;

        var panelState = new ResizablePanelState(intId, vboxState, clipPushed);
        Context.Layout.PushContainer(panelState);
    }

    public static void EndResizableVPanel()
    {
        if (Context.Layout.ContainerStackCount == 0 || Context.Layout.PeekContainer() is not ResizablePanelState state)
        { Console.WriteLine("Error: EndResizableVPanel called without a matching BeginResizableVPanel."); return; }
        if (state.ClipRectWasPushed)
        {
            Context.Layout.PopClipRect(); // For culling
            Context.Renderer.PopClipRect();
        }
        Context.Layout.PopContainer();
    }

    public static void BeginResizableHPanel(
        string id,
        ref float currentHeight,
        float reservedLeftSpace,
        float reservedRightSpace,
        float topOffset = 0f,
        float minHeight = 50f,
        float maxHeight = 300f,
        float resizeHandleHeight = 5f,
        BoxStyle? panelStyle = null,
        Vector2 padding = default,
        float gap = 5f,
        bool disabled = false)
    {
        if (!IsContextValid()) return;
        var intId = id.GetHashCode();

        Vector2 finalPadding = (padding == default) ? new Vector2(5, 5) : padding;

        var input = Context.InputState;
        var renderer = Context.Renderer;
        var windowWidth = renderer.RenderTargetSize.X;
        var windowHeight = renderer.RenderTargetSize.Y;
        var availableWidth = Math.Max(0, windowWidth - reservedLeftSpace - reservedRightSpace);
        var maxAllowedHeight = windowHeight - topOffset;
        var effectiveMaxHeight = Math.Min(maxHeight, maxAllowedHeight);
        float clampMax = Math.Max(minHeight, effectiveMaxHeight);

        if (!disabled)
        {
            currentHeight = Math.Clamp(currentHeight, minHeight, clampMax);
            float panelY = windowHeight - currentHeight;
            float handleHeight = Math.Min(resizeHandleHeight, currentHeight);
            Rect handleRect = new Rect(reservedLeftSpace, panelY, availableWidth, handleHeight);

            bool isHoveringHandle = handleRect.Contains(input.MousePosition.X, input.MousePosition.Y);
            if (isHoveringHandle) State.SetPotentialInputTarget(intId);

            if (input.WasLeftMousePressedThisFrame && isHoveringHandle && State.PotentialInputTargetId == intId && !State.DragInProgressFromPreviousFrame)
            {
                State.TrySetActivePress(intId, 10);
            }

            if (State.ActivelyPressedElementId == intId && !input.IsLeftMouseDown) State.ClearActivePress(intId);

            if (State.ActivelyPressedElementId == intId && input.IsLeftMouseDown)
            {
                float clampedMouseY = Math.Max(input.MousePosition.Y, topOffset);
                currentHeight = Math.Clamp(windowHeight - clampedMouseY, minHeight, clampMax);
            }
        }

        var finalPanelStyle = panelStyle ?? new BoxStyle { FillColor = new(0.15f, 0.15f, 0.2f, 1.0f), BorderColor = DefaultTheme.NormalBorder, BorderLength = 1 };
        currentHeight = Math.Clamp(currentHeight, minHeight, clampMax);
        float finalPanelY = windowHeight - currentHeight;
        Rect panelRect = new Rect(reservedLeftSpace, finalPanelY, availableWidth, currentHeight);
        if (panelRect.Width > 0 && panelRect.Height > 0)
        {
            renderer.DrawBox(new Rect(panelRect.X, panelRect.Y, panelRect.Width, panelRect.Height), finalPanelStyle);
        }

        Vector2 contentStartPosition = new Vector2(reservedLeftSpace + finalPadding.X, finalPanelY + finalPadding.Y);
        Rect contentClipRect = new Rect(contentStartPosition.X, contentStartPosition.Y, Math.Max(0, availableWidth - (finalPadding.X * 2)), Math.Max(0, currentHeight - (finalPadding.Y * 2)));
        bool clipPushed = false;
        if (contentClipRect.Width > 0 && contentClipRect.Height > 0)
        {
            renderer.PushClipRect(contentClipRect, D2D.AntialiasMode.Aliased);
            Context.Layout.PushClipRect(contentClipRect); // For culling
            clipPushed = true;
        }

        var hboxId = HashCode.Combine(intId, "_hbox");
        var hboxState = Context.Layout.GetOrCreateHBoxState(hboxId);
        hboxState.StartPosition = contentStartPosition;
        hboxState.CurrentPosition = contentStartPosition;
        hboxState.Gap = gap;
        hboxState.MaxElementHeight = 0f;
        hboxState.AccumulatedWidth = 0f;
        hboxState.ElementCount = 0;

        var panelState = new ResizableHPanelState(intId, hboxState, clipPushed);
        Context.Layout.PushContainer(panelState);
    }

    public static void EndResizableHPanel()
    {
        if (Context.Layout.ContainerStackCount == 0 || Context.Layout.PeekContainer() is not ResizableHPanelState state)
        { Console.WriteLine("Error: EndResizableHPanel called without a matching BeginResizableHPanel."); return; }
        if (state.ClipRectWasPushed)
        {
            Context.Layout.PopClipRect(); // For culling
            Context.Renderer.PopClipRect();
        }
        Context.Layout.PopContainer();
    }
}