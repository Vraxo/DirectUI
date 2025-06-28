// Core/UI.Containers.cs
using System;
using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Mathematics;
using D2D = Vortice.Direct2D1;

namespace DirectUI;

public static partial class UI
{
    public static void BeginHBoxContainer(int id, Vector2 position, float gap = 5.0f)
    {
        Context.Layout.BeginHBox(id, position, gap);
    }

    public static void EndHBoxContainer()
    {
        if (Context.Layout.ContainerStackCount == 0 || Context.Layout.PeekContainer() is not HBoxContainerState state)
        { Console.WriteLine("Error: EndHBoxContainer called without a matching BeginHBoxContainer."); return; }
        Context.Layout.PopContainer();
        if (Context.Layout.IsInLayoutContainer())
        { Context.Layout.AdvanceContainerLayout(new Vector2(state.AccumulatedWidth, state.MaxElementHeight)); }
    }

    public static void BeginVBoxContainer(int id, Vector2 position, float gap = 5.0f)
    {
        Context.Layout.BeginVBox(id, position, gap);
    }

    public static void EndVBoxContainer()
    {
        if (Context.Layout.ContainerStackCount == 0 || Context.Layout.PeekContainer() is not VBoxContainerState state)
        { Console.WriteLine("Error: EndVBoxContainer called without a matching BeginVBoxContainer."); return; }
        Context.Layout.PopContainer();
        if (Context.Layout.IsInLayoutContainer())
        { Context.Layout.AdvanceContainerLayout(new Vector2(state.MaxElementWidth, state.AccumulatedHeight)); }
    }

    public static void BeginGridContainer(int id, Vector2 position, Vector2 availableSize, int numColumns, Vector2 gap)
    {
        Context.Layout.PushContainer(new GridContainerState(id, position, availableSize, numColumns, gap));
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

    public static void BeginResizableVPanel(int id, ref float currentWidth, ResizablePanelDefinition definition, HAlignment alignment = HAlignment.Left, float topOffset = 0f)
    {
        if (!IsContextValid() || definition == null) return;
        var intId = id;

        var input = Context.InputState;
        var renderTarget = Context.RenderTarget;
        var windowWidth = renderTarget.Size.Width;
        var windowHeight = renderTarget.Size.Height;
        var availableHeight = windowHeight - topOffset;

        if (!definition.Disabled)
        {
            float handleWidth = Math.Min(definition.ResizeHandleWidth, currentWidth);
            float panelX = (alignment == HAlignment.Right) ? windowWidth - currentWidth : 0;
            float handleX = (alignment == HAlignment.Right) ? panelX : panelX + currentWidth - handleWidth;
            Rect handleRect = new Rect(handleX, topOffset, handleWidth, availableHeight);

            bool isHoveringHandle = handleRect.Contains(input.MousePosition.X, input.MousePosition.Y);
            if (isHoveringHandle) State.SetPotentialInputTarget(intId);
            if (input.WasLeftMousePressedThisFrame && isHoveringHandle && State.PotentialInputTargetId == intId && !State.DragInProgressFromPreviousFrame) State.SetPotentialCaptorForFrame(intId);
            if (State.ActivelyPressedElementId == intId && !input.IsLeftMouseDown) State.ClearActivePress(intId);
            if (State.ActivelyPressedElementId == intId && input.IsLeftMouseDown)
            {
                if (alignment == HAlignment.Left) currentWidth = Math.Clamp(input.MousePosition.X, definition.MinWidth, definition.MaxWidth);
                else currentWidth = Math.Clamp(windowWidth - input.MousePosition.X, definition.MinWidth, definition.MaxWidth);
            }
        }

        var panelStyle = definition.PanelStyle ?? new BoxStyle { FillColor = new(0.15f, 0.15f, 0.2f, 1.0f), BorderColor = DefaultTheme.NormalBorder, BorderLength = 1 };
        currentWidth = Math.Max(0, currentWidth);
        float finalPanelX = (alignment == HAlignment.Right) ? windowWidth - currentWidth : 0;
        Rect panelRect = new Rect(finalPanelX, topOffset, currentWidth, availableHeight);
        if (panelRect.Width > 0 && panelRect.Height > 0)
        {
            Resources.DrawBoxStyleHelper(renderTarget, new Vector2(panelRect.X, panelRect.Y), new Vector2(panelRect.Width, panelRect.Height), panelStyle);
        }

        Vector2 contentStartPosition = new Vector2(finalPanelX + definition.Padding.X, topOffset + definition.Padding.Y);
        Rect contentClipRect = new Rect(contentStartPosition.X, contentStartPosition.Y, Math.Max(0, currentWidth - (definition.Padding.X * 2)), Math.Max(0, availableHeight - (definition.Padding.Y * 2)));
        bool clipPushed = false;
        if (contentClipRect.Width > 0 && contentClipRect.Height > 0)
        {
            renderTarget.PushAxisAlignedClip(contentClipRect, D2D.AntialiasMode.Aliased);
            clipPushed = true;
        }

        var vboxId = HashCode.Combine(id, "_vbox");
        var vboxState = Context.Layout.GetOrCreateVBoxState(vboxId);
        vboxState.StartPosition = contentStartPosition;
        vboxState.CurrentPosition = contentStartPosition;
        vboxState.Gap = definition.Gap;
        vboxState.MaxElementWidth = 0f;
        vboxState.AccumulatedHeight = 0f;
        vboxState.ElementCount = 0;

        var panelState = new ResizablePanelState(id, vboxState, clipPushed);
        Context.Layout.PushContainer(panelState);
    }

    public static void EndResizableVPanel()
    {
        if (Context.Layout.ContainerStackCount == 0 || Context.Layout.PeekContainer() is not ResizablePanelState state)
        { Console.WriteLine("Error: EndResizableVPanel called without a matching BeginResizableVPanel."); return; }
        if (state.ClipRectWasPushed && Context.RenderTarget is not null)
        {
            Context.RenderTarget.PopAxisAlignedClip();
        }
        Context.Layout.PopContainer();
    }

    public static void BeginResizableHPanel(int id, ref float currentHeight, ResizableHPanelDefinition definition, float reservedLeftSpace, float reservedRightSpace, float topOffset = 0f)
    {
        if (!IsContextValid() || definition == null) return;
        var intId = id;

        var input = Context.InputState;
        var renderTarget = Context.RenderTarget;
        var windowWidth = renderTarget.Size.Width;
        var windowHeight = renderTarget.Size.Height;
        var availableWidth = Math.Max(0, windowWidth - reservedLeftSpace - reservedRightSpace);
        var maxAllowedHeight = windowHeight - topOffset;
        var effectiveMaxHeight = Math.Min(definition.MaxHeight, maxAllowedHeight);

        // Fix: Ensure the max value for clamping is not less than the min value.
        // This prevents a crash when the window is resized to a very small height.
        float clampMax = Math.Max(definition.MinHeight, effectiveMaxHeight);

        if (!definition.Disabled)
        {
            currentHeight = Math.Clamp(currentHeight, definition.MinHeight, clampMax);
            float panelY = windowHeight - currentHeight;
            float handleHeight = Math.Min(definition.ResizeHandleWidth, currentHeight);
            Rect handleRect = new Rect(reservedLeftSpace, panelY, availableWidth, handleHeight);

            bool isHoveringHandle = handleRect.Contains(input.MousePosition.X, input.MousePosition.Y);
            if (isHoveringHandle) State.SetPotentialInputTarget(intId);
            if (input.WasLeftMousePressedThisFrame && isHoveringHandle && State.PotentialInputTargetId == intId && !State.DragInProgressFromPreviousFrame) State.SetPotentialCaptorForFrame(intId);
            if (State.ActivelyPressedElementId == intId && !input.IsLeftMouseDown) State.ClearActivePress(intId);
            if (State.ActivelyPressedElementId == intId && input.IsLeftMouseDown)
            {
                float clampedMouseY = Math.Max(input.MousePosition.Y, topOffset);
                currentHeight = Math.Clamp(windowHeight - clampedMouseY, definition.MinHeight, clampMax);
            }
        }

        var panelStyle = definition.PanelStyle ?? new BoxStyle { FillColor = new(0.15f, 0.15f, 0.2f, 1.0f), BorderColor = DefaultTheme.NormalBorder, BorderLength = 1 };
        currentHeight = Math.Clamp(currentHeight, definition.MinHeight, clampMax);
        float finalPanelY = windowHeight - currentHeight;
        Rect panelRect = new Rect(reservedLeftSpace, finalPanelY, availableWidth, currentHeight);
        if (panelRect.Width > 0 && panelRect.Height > 0)
        {
            Resources.DrawBoxStyleHelper(renderTarget, new Vector2(panelRect.X, panelRect.Y), new Vector2(panelRect.Width, panelRect.Height), panelStyle);
        }

        Vector2 contentStartPosition = new Vector2(reservedLeftSpace + definition.Padding.X, finalPanelY + definition.Padding.Y);
        Rect contentClipRect = new Rect(contentStartPosition.X, contentStartPosition.Y, Math.Max(0, availableWidth - (definition.Padding.X * 2)), Math.Max(0, currentHeight - (definition.Padding.Y * 2)));
        bool clipPushed = false;
        if (contentClipRect.Width > 0 && contentClipRect.Height > 0)
        {
            renderTarget.PushAxisAlignedClip(contentClipRect, D2D.AntialiasMode.Aliased);
            clipPushed = true;
        }

        var hboxId = HashCode.Combine(id, "_hbox");
        var hboxState = Context.Layout.GetOrCreateHBoxState(hboxId);
        hboxState.StartPosition = contentStartPosition;
        hboxState.CurrentPosition = contentStartPosition;
        hboxState.Gap = definition.Gap;
        hboxState.MaxElementHeight = 0f;
        hboxState.AccumulatedWidth = 0f;
        hboxState.ElementCount = 0;

        var panelState = new ResizableHPanelState(id, hboxState, clipPushed);
        Context.Layout.PushContainer(panelState);
    }

    public static void EndResizableHPanel()
    {
        if (Context.Layout.ContainerStackCount == 0 || Context.Layout.PeekContainer() is not ResizableHPanelState state)
        { Console.WriteLine("Error: EndResizableHPanel called without a matching BeginResizableHPanel."); return; }
        if (state.ClipRectWasPushed && Context.RenderTarget is not null)
        {
            Context.RenderTarget.PopAxisAlignedClip();
        }
        Context.Layout.PopContainer();
    }
}