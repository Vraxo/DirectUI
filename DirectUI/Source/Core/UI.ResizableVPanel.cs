using System;
using System.Numerics;
using Vortice.Mathematics;
using D2D = Vortice.Direct2D1;

namespace DirectUI;

public static partial class UI
{
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
        var state = State; // Define local state variable

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
            if (isHoveringHandle) state.SetPotentialInputTarget(intId);

            if (input.WasLeftMousePressedThisFrame && isHoveringHandle && state.PotentialInputTargetId == intId && !state.DragInProgressFromPreviousFrame)
            {
                state.TrySetActivePress(intId, 10);
            }

            if (state.ActivelyPressedElementId == intId && !input.IsLeftMouseDown) state.ClearActivePress(intId);

            if (state.ActivelyPressedElementId == intId && input.IsLeftMouseDown)
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
}