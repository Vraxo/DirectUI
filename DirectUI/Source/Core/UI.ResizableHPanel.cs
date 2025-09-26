// Core/UI.Containers.cs
using System;
using System.Numerics;
using Vortice.Mathematics;
using D2D = Vortice.Direct2D1;

namespace DirectUI;

public static partial class UI
{
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
        var state = State; // Define local state variable

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
            if (isHoveringHandle) state.SetPotentialInputTarget(intId);

            if (input.WasLeftMousePressedThisFrame && isHoveringHandle && state.PotentialInputTargetId == intId && !state.DragInProgressFromPreviousFrame)
            {
                state.TrySetActivePress(intId, 10);
            }

            if (state.ActivelyPressedElementId == intId && !input.IsLeftMouseDown) state.ClearActivePress(intId);

            if (state.ActivelyPressedElementId == intId && input.IsLeftMouseDown)
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