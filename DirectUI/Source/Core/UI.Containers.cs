using System;
using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Mathematics;
using static System.Net.Mime.MediaTypeNames;
using D2D = Vortice.Direct2D1;

namespace DirectUI;

public static partial class UI
{
    // --- Containers ---
    public static void BeginHBoxContainer(string id, Vector2 position, float gap = 5.0f)
    {
        var containerState = new HBoxContainerState(id, position, gap);
        containerStack.Push(containerState);
    }
    public static void EndHBoxContainer()
    {
        if (containerStack.Count == 0 || containerStack.Peek() is not HBoxContainerState state)
        { Console.WriteLine("Error: EndHBoxContainer called without a matching BeginHBoxContainer."); return; }
        containerStack.Pop();
        if (IsInLayoutContainer())
        { AdvanceLayoutCursor(new Vector2(state.AccumulatedWidth, state.MaxElementHeight)); }
    }
    public static void BeginVBoxContainer(string id, Vector2 position, float gap = 5.0f)
    {
        var containerState = new VBoxContainerState(id, position, gap);
        containerStack.Push(containerState);
    }
    public static void EndVBoxContainer()
    {
        if (containerStack.Count == 0 || containerStack.Peek() is not VBoxContainerState state)
        { Console.WriteLine("Error: EndVBoxContainer called without a matching BeginVBoxContainer."); return; }
        containerStack.Pop();
        if (IsInLayoutContainer())
        { AdvanceLayoutCursor(new Vector2(state.MaxElementWidth, state.AccumulatedHeight)); }
    }
    public static void BeginGridContainer(string id, Vector2 position, Vector2 availableSize, int numColumns, Vector2 gap)
    {
        var containerState = new GridContainerState(id, position, availableSize, numColumns, gap);
        containerStack.Push(containerState);
    }
    public static void EndGridContainer()
    {
        if (containerStack.Count == 0 || containerStack.Peek() is not GridContainerState state)
        { Console.WriteLine("Error: EndGridContainer called without a matching BeginGridContainer."); return; }
        containerStack.Pop();
        if (IsInLayoutContainer())
        {
            if (state.RowHeights.Count > 0 && state.CurrentCellIndex > 0)
            {
                int lastPopulatedRowIndex = (state.CurrentCellIndex - 1) / state.NumColumns;
                if (lastPopulatedRowIndex < state.RowHeights.Count)
                { state.RowHeights[lastPopulatedRowIndex] = state.CurrentRowMaxHeight; }
                else { Console.WriteLine($"Warning: Grid '{state.Id}' - Row index mismatch during EndGridContainer."); }
                state.AccumulatedHeight = 0f; bool firstRowAdded = false;
                for (int i = 0; i < state.RowHeights.Count; i++) { if (state.RowHeights[i] > 0) { if (firstRowAdded) { state.AccumulatedHeight += state.Gap.Y; } state.AccumulatedHeight += state.RowHeights[i]; firstRowAdded = true; } }
            }
            Vector2 containerSize = state.GetTotalOccupiedSize();
            AdvanceLayoutCursor(containerSize);
        }
    }

    public static void BeginResizableVPanel(string id, ref float currentWidth, ResizablePanelDefinition definition, HAlignment alignment = HAlignment.Left, float topOffset = 0f)
    {
        if (!IsContextValid() || definition == null) return;
        var intId = id.GetHashCode();

        var input = CurrentInputState;
        var renderTarget = CurrentRenderTarget!;
        var windowWidth = renderTarget.Size.Width;
        var windowHeight = renderTarget.Size.Height;
        var availableHeight = windowHeight - topOffset;

        // --- Input and Resizing Logic ---
        if (!definition.Disabled)
        {
            float handleWidth = definition.ResizeHandleWidth;
            float panelX = (alignment == HAlignment.Right) ? windowWidth - currentWidth : 0;
            handleWidth = Math.Min(handleWidth, currentWidth);
            float handleX = (alignment == HAlignment.Right) ? panelX : panelX + currentWidth - handleWidth;
            Rect handleRect = new Rect(handleX, topOffset, handleWidth, availableHeight);

            bool isHoveringHandle = handleRect.Contains(input.MousePosition.X, input.MousePosition.Y);
            if (isHoveringHandle) SetPotentialInputTarget(intId);
            if (input.WasLeftMousePressedThisFrame && isHoveringHandle && PotentialInputTargetId == intId && !dragInProgressFromPreviousFrame) SetPotentialCaptorForFrame(intId);
            if (ActivelyPressedElementId == intId && !input.IsLeftMouseDown) ClearActivePress(intId);
            if (ActivelyPressedElementId == intId && input.IsLeftMouseDown)
            {
                if (alignment == HAlignment.Left) currentWidth = Math.Clamp(input.MousePosition.X, definition.MinWidth, definition.MaxWidth);
                else currentWidth = Math.Clamp(windowWidth - input.MousePosition.X, definition.MinWidth, definition.MaxWidth);
            }
        }

        // --- Drawing ---
        var panelStyle = definition.PanelStyle ?? new BoxStyle { FillColor = new(0.15f, 0.15f, 0.2f, 1.0f), BorderColor = DefaultTheme.NormalBorder, BorderLength = 1 };
        currentWidth = Math.Max(0, currentWidth);
        float finalPanelX = (alignment == HAlignment.Right) ? windowWidth - currentWidth : 0;
        Rect panelRect = new Rect(finalPanelX, topOffset, currentWidth, availableHeight);
        if (panelRect.Width > 0 && panelRect.Height > 0)
        {
            DrawBoxStyleHelper(renderTarget, new Vector2(panelRect.X, panelRect.Y), new Vector2(panelRect.Width, panelRect.Height), panelStyle);
        }

        // --- Container & Clipping Logic ---
        Vector2 contentStartPosition = new Vector2(finalPanelX + definition.Padding.X, topOffset + definition.Padding.Y);
        Rect contentClipRect = new Rect(contentStartPosition.X, contentStartPosition.Y, Math.Max(0, currentWidth - (definition.Padding.X * 2)), Math.Max(0, availableHeight - (definition.Padding.Y * 2)));
        bool clipPushed = false;
        if (contentClipRect.Width > 0 && contentClipRect.Height > 0)
        {
            renderTarget.PushAxisAlignedClip(contentClipRect, D2D.AntialiasMode.Aliased);
            clipPushed = true;
        }
        var vboxState = new VBoxContainerState(id + "_vbox", contentStartPosition, definition.Gap);
        var panelState = new ResizablePanelState(id, vboxState, clipPushed);
        containerStack.Push(panelState);
    }

    public static void EndResizableVPanel()
    {
        if (containerStack.Count == 0 || containerStack.Peek() is not ResizablePanelState state)
        {
            Console.WriteLine("Error: EndResizableVPanel called without a matching BeginResizableVPanel.");
            return;
        }

        if (state.ClipRectWasPushed && CurrentRenderTarget is not null)
        {
            CurrentRenderTarget.PopAxisAlignedClip();
        }

        containerStack.Pop();
    }

    public static void BeginResizableHPanel(string id, ref float currentHeight, ResizableHPanelDefinition definition, float reservedLeftSpace, float reservedRightSpace, float topOffset = 0f)
    {
        if (!IsContextValid() || definition == null) return;
        var intId = id.GetHashCode();

        var input = CurrentInputState;
        var renderTarget = CurrentRenderTarget!;
        var windowWidth = renderTarget.Size.Width;
        var windowHeight = renderTarget.Size.Height;
        var availableWidth = Math.Max(0, windowWidth - reservedLeftSpace - reservedRightSpace);
        var maxAllowedHeight = windowHeight - topOffset;
        var effectiveMaxHeight = Math.Min(definition.MaxHeight, maxAllowedHeight);


        // --- Input and Resizing Logic ---
        if (!definition.Disabled)
        {
            currentHeight = Math.Clamp(currentHeight, definition.MinHeight, effectiveMaxHeight);

            float panelY = windowHeight - currentHeight;
            float handleHeight = definition.ResizeHandleWidth;
            handleHeight = Math.Min(handleHeight, currentHeight);
            Rect handleRect = new Rect(reservedLeftSpace, panelY, availableWidth, handleHeight);

            bool isHoveringHandle = handleRect.Contains(input.MousePosition.X, input.MousePosition.Y);
            if (isHoveringHandle) SetPotentialInputTarget(intId);
            if (input.WasLeftMousePressedThisFrame && isHoveringHandle && PotentialInputTargetId == intId && !dragInProgressFromPreviousFrame) SetPotentialCaptorForFrame(intId);
            if (ActivelyPressedElementId == intId && !input.IsLeftMouseDown) ClearActivePress(intId);
            if (ActivelyPressedElementId == intId && input.IsLeftMouseDown)
            {
                float clampedMouseY = Math.Max(input.MousePosition.Y, topOffset);
                currentHeight = Math.Clamp(windowHeight - clampedMouseY, definition.MinHeight, effectiveMaxHeight);
            }
        }

        // --- Drawing ---
        var panelStyle = definition.PanelStyle ?? new BoxStyle { FillColor = new(0.15f, 0.15f, 0.2f, 1.0f), BorderColor = DefaultTheme.NormalBorder, BorderLength = 1 };
        currentHeight = Math.Clamp(currentHeight, definition.MinHeight, effectiveMaxHeight);
        float finalPanelY = windowHeight - currentHeight;
        Rect panelRect = new Rect(reservedLeftSpace, finalPanelY, availableWidth, currentHeight);
        if (panelRect.Width > 0 && panelRect.Height > 0)
        {
            DrawBoxStyleHelper(renderTarget, new Vector2(panelRect.X, panelRect.Y), new Vector2(panelRect.Width, panelRect.Height), panelStyle);
        }

        // --- Container & Clipping Logic ---
        Vector2 contentStartPosition = new Vector2(reservedLeftSpace + definition.Padding.X, finalPanelY + definition.Padding.Y);
        Rect contentClipRect = new Rect(contentStartPosition.X, contentStartPosition.Y, Math.Max(0, availableWidth - (definition.Padding.X * 2)), Math.Max(0, currentHeight - (definition.Padding.Y * 2)));
        bool clipPushed = false;
        if (contentClipRect.Width > 0 && contentClipRect.Height > 0)
        {
            renderTarget.PushAxisAlignedClip(contentClipRect, D2D.AntialiasMode.Aliased);
            clipPushed = true;
        }
        var hboxState = new HBoxContainerState(id + "_hbox", contentStartPosition, definition.Gap);
        var panelState = new ResizableHPanelState(id, hboxState, clipPushed);
        containerStack.Push(panelState);
    }

    public static void EndResizableHPanel()
    {
        if (containerStack.Count == 0 || containerStack.Peek() is not ResizableHPanelState state)
        {
            Console.WriteLine("Error: EndResizableHPanel called without a matching BeginResizableHPanel.");
            return;
        }

        if (state.ClipRectWasPushed && CurrentRenderTarget is not null)
        {
            CurrentRenderTarget.PopAxisAlignedClip();
        }

        containerStack.Pop();
    }
}