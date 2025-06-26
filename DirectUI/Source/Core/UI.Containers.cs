using System;
using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Mathematics;
using D2D = Vortice.Direct2D1;

namespace DirectUI;

public static partial class UI
{
    // --- Containers ---
    public static void BeginHBoxContainer(string id, Vector2 position, float gap = 5.0f)
    {
        Vector2 startPosition = ApplyLayout(position);
        var containerState = new HBoxContainerState(id, startPosition, gap);
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
        Vector2 startPosition = ApplyLayout(position);
        var containerState = new VBoxContainerState(id, startPosition, gap);
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
        Vector2 startPosition = ApplyLayout(position);
        var containerState = new GridContainerState(id, startPosition, availableSize, numColumns, gap);
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

    public static void BeginResizableVPanel(string id, ref float currentWidth, ResizablePanelDefinition definition)
    {
        if (!IsContextValid() || definition == null) return;

        var input = CurrentInputState;
        var renderTarget = CurrentRenderTarget!;
        var windowHeight = renderTarget.Size.Height;

        // --- Input and Resizing Logic ---
        if (!definition.Disabled)
        {
            float handleWidth = definition.ResizeHandleWidth;
            // Ensure handle is not wider than the panel itself
            handleWidth = Math.Min(handleWidth, currentWidth);
            Rect handleRect = new Rect(currentWidth - handleWidth, 0, handleWidth, windowHeight);

            bool isHoveringHandle = handleRect.Contains(input.MousePosition.X, input.MousePosition.Y);

            if (isHoveringHandle)
            {
                SetPotentialInputTarget(id);
            }

            // Start resizing
            if (input.WasLeftMousePressedThisFrame && isHoveringHandle && PotentialInputTargetId == id && !dragInProgressFromPreviousFrame)
            {
                SetPotentialCaptorForFrame(id);
            }

            // Stop resizing on mouse up
            if (ActivelyPressedElementId == id && !input.IsLeftMouseDown)
            {
                ClearActivePress(id);
            }

            // Update width while resizing (mouse is down and we are the active element)
            if (ActivelyPressedElementId == id && input.IsLeftMouseDown)
            {
                currentWidth = Math.Clamp(input.MousePosition.X, definition.MinWidth, definition.MaxWidth);
            }
        }

        // --- Drawing ---
        var panelStyle = definition.PanelStyle ?? new BoxStyle { FillColor = new(0.15f, 0.15f, 0.2f, 1.0f), BorderColor = DefaultTheme.NormalBorder, BorderLength = 1 };

        currentWidth = Math.Max(0, currentWidth);
        Rect panelRect = new Rect(0, 0, currentWidth, windowHeight);

        if (panelRect.Width > 0)
        {
            DrawBoxStyleHelper(renderTarget, new Vector2(panelRect.X, panelRect.Y), new Vector2(panelRect.Width, panelRect.Height), panelStyle);
        }

        // --- Container & Clipping Logic ---
        Vector2 contentStartPosition = new Vector2(definition.Padding.X, definition.Padding.Y);

        Rect contentClipRect = new Rect(
            contentStartPosition.X,
            contentStartPosition.Y,
            Math.Max(0, currentWidth - (definition.Padding.X * 2)),
            Math.Max(0, windowHeight - (definition.Padding.Y * 2))
        );

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
}