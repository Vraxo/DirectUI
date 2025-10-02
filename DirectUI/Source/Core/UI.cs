// Entire file content here
using System;
using System.Numerics;
using DirectUI.Core;
using DirectUI.Drawing;
using Vortice.Direct2D1;
using Vortice.Mathematics;

namespace DirectUI;

public static partial class UI
{
    // --- Core Components ---
    public static UIContext Context { get; private set; } = null!;
    public static UIPersistentState State => Context.State;
    public static bool IsRendering
    {
        get; private set;
    } = false;

    /// <summary>
    /// A private, internal renderer that performs no-op drawing calls.
    /// Used by CalculateLayout to run UI logic for measurement purposes only,
    /// without generating any GPU commands.
    /// </summary>
    private class NullRenderer : IRenderer
    {
        public Vector2 RenderTargetSize { get; }

        public NullRenderer(Vector2 renderTargetSize)
        {
            RenderTargetSize = renderTargetSize;
        }

        public void DrawBox(Rect rect, BoxStyle style) { }
        public void DrawImage(byte[] imageData, string imageKey, Rect destination) { }
        public void DrawLine(Vector2 p1, Vector2 p2, Color color, float strokeWidth) { }
        public void DrawText(Vector2 origin, string text, ButtonStyle style, Alignment alignment, Vector2 maxSize, Color color) { }
        public void Flush() { }
        public void PopClipRect(Rect rect, AntialiasMode antialiasMode = AntialiasMode.PerPrimitive) { }
        public void PushClipRect(Rect rect, AntialiasMode antialiasMode = AntialiasMode.PerPrimitive) { }

        public void PopClipRect()
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Executes a block of UI logic in a calculation-only mode to determine its final size
    /// without performing any actual rendering. This is useful for sizing containers based on
    /// their dynamic content before drawing them.
    /// </summary>
    /// <param name="layoutCode">An action containing the UI calls to be measured.</param>
    /// <returns>The final logical size of the content within the provided action.</returns>
    public static Vector2 CalculateLayout(Action layoutCode)
    {
        if (!IsContextValid()) return Vector2.Zero;

        var originalLayout = Context.Layout;
        var originalRenderer = Context.Renderer;
        var originalIsLayoutPass = Context.IsLayoutPass;

        // Create a new layout manager for this calculation to keep it isolated.
        var calculationLayout = new UILayoutManager(Context.UIScale);
        // Put a root VBox in it to measure the total size of whatever the user does.
        calculationLayout.BeginVBox("calc_root".GetHashCode(), Vector2.Zero, 0);

        // Swap context properties for the calculation pass
        Context.Layout = calculationLayout;
        Context.Renderer = new NullRenderer(originalRenderer.RenderTargetSize);
        Context.IsLayoutPass = true; // SET FLAG

        try
        {
            // Run user's UI code. It will only perform layout actions and will not modify persistent state.
            layoutCode();
        }
        finally
        {
            // Restore original context state
            Context.Layout = originalLayout;
            Context.Renderer = originalRenderer;
            Context.IsLayoutPass = originalIsLayoutPass; // RESTORE FLAG
        }

        // Pop the root container to get its final calculated state.
        if (calculationLayout.ContainerStackCount > 0)
        {
            var measuredVBox = (VBoxContainerState)calculationLayout.PopContainer();
            return measuredVBox.GetAccumulatedSize();
        }

        return Vector2.Zero;
    }

    // --- Frame Management ---
    public static void BeginFrame(UIContext context)
    {
        IsRendering = true;

        UI.Context = context;

        // Update animations with the current time before any UI logic runs.
        context.State.AnimationManager.Update(context.TotalTime);

        context.State.ResetFrameState(context.InputState);

        UI.Context.Layout.ClearStack();
        UI.Context.treeStateStack.Clear();
    }

    public static void EndFrame()
    {
        // At the end of the frame, resolve the winner from all click requests made this frame.
        // This winner's action (for Press mode) will be triggered in the next frame.
        var pressWinner = UI.State.ClickCaptureServer.GetWinner();
        if (pressWinner.HasValue)
        {
            UI.State.SetNextFramePressWinner(pressWinner.Value);
        }

        // If a click happened but no UI element captured it, and no popup was open, clear focus.
        if (UI.Context.InputState.WasLeftMousePressedThisFrame && UI.State.InputCaptorId == 0 && !UI.State.IsPopupOpen)
        {
            UI.State.SetFocus(0);
        }

        HandlePopupLogic();

        // After all widgets are processed, check for stale active press state.
        // If the mouse is up, but a widget is still marked as 'actively pressed',
        // it means that widget was not drawn this frame (e.g. it disappeared),
        // so we must clear the state globally to prevent it getting stuck.
        if (!UI.Context.InputState.IsLeftMouseDown && UI.State.ActivelyPressedElementId != 0)
        {
            UI.State.ClearAllActivePressState();
        }

        if (UI.Context.Layout.ContainerStackCount > 0)
        {
            Console.WriteLine($"Warning: Mismatch in Begin/End container calls. {UI.Context.Layout.ContainerStackCount} containers left open at EndFrame.");
            UI.Context.Layout.ClearStack();
        }
        if (UI.Context.treeStateStack.Count > 0)
        {
            Console.WriteLine($"Warning: Mismatch in Begin/End Tree calls. {UI.Context.treeStateStack.Count} trees left open at EndFrame.");
            UI.Context.treeStateStack.Clear();
        }

        // It's important that IsRendering is set to false AFTER the context is cleared.
        UI.Context = null!;
        IsRendering = false;
    }

    /// <summary>
    /// Handles popup logic at the end of a frame, ensuring they are drawn last and closed correctly.
    /// </summary>
    private static void HandlePopupLogic()
    {
        if (!UI.State.IsPopupOpen) return;

        // If a mouse press occurred outside the popup's bounds, close the popup.
        // This logic is skipped on the same frame the popup was opened to prevent it from closing immediately.
        if (!UI.State.PopupWasOpenedThisFrame && (UI.Context.InputState.WasLeftMousePressedThisFrame || UI.Context.InputState.WasRightMousePressedThisFrame))
        {
            if (!UI.State.PopupBounds.Contains(UI.Context.InputState.MousePosition))
            {
                UI.State.ClearActivePopup();
                return; // Don't draw the popup since we just closed it.
            }
        }

        // Execute the callback to draw the popup content.
        UI.State.PopupDrawCallback?.Invoke(UI.Context);
    }

    /// <summary>
    /// Checks if a context menu should be opened for a given widget ID.
    /// A context menu is typically triggered by a right-click.
    /// </summary>
    /// <param name="widgetId">The unique ID of the widget that can open the context menu.</param>
    /// <returns>True if the context menu should be opened this frame, false otherwise.</returns>
    public static bool BeginContextMenu(string widgetId)
    {
        if (!IsContextValid()) return false;

        int intId = widgetId.GetHashCode();
        var state = UI.State;
        var input = UI.Context.InputState;

        // A context menu is opened if the right mouse button was pressed this frame,
        // and the potential input target is the widget we're checking.
        if (input.WasRightMousePressedThisFrame && state.PotentialInputTargetId == intId)
        {
            state.ClearActivePopup(); // Close any other popups first.
            return true;
        }

        return false;
    }

    /// <summary>
    /// Draws a context menu popup at the current mouse position. This should be called after BeginContextMenu returns true.
    /// </summary>
    /// <param name="popupId">A unique ID for this specific context menu instance.</param>
    /// <param name="items">An array of strings representing the menu items.</param>
    /// <returns>The index of the clicked item, or -1 if no item was clicked.</returns>
    public static int ContextMenu(string popupId, string[] items)
    {
        if (!IsContextValid() || items is null || items.Length == 0) return -1;

        var context = UI.Context;
        var state = UI.State;
        int intId = popupId.GetHashCode();

        // If a result for this menu is already available from the previous frame, consume and return it.
        if (state.PopupResultAvailable && state.PopupResultOwnerId == intId)
        {
            return state.PopupResult;
        }

        // Only set up the popup on the first frame it's requested.
        // After that, the EndFrame logic will handle drawing it using the stored state.
        if (!state.IsPopupOpen || state.ActivePopupId != intId)
        {
            // Calculate popup properties ONCE
            float itemHeight = 25;
            float itemWidth = 150;
            float popupHeight = items.Length * itemHeight;
            var popupPosition = context.InputState.MousePosition;
            var popupBounds = new Vortice.Mathematics.Rect(popupPosition.X, popupPosition.Y, itemWidth, popupHeight);

            // Define the draw callback for the popup, which runs at EndFrame
            Action<UIContext> drawCallback = (ctx) =>
            {
                var popupStyle = new BoxStyle { FillColor = DefaultTheme.NormalFill, BorderColor = DefaultTheme.FocusBorder, BorderLength = 1f, Roundness = 0.1f };
                ctx.Renderer.DrawBox(popupBounds, popupStyle);

                for (int i = 0; i < items.Length; i++)
                {
                    var itemBounds = new Vortice.Mathematics.Rect(popupBounds.X, popupBounds.Y + i * itemHeight, popupBounds.Width, itemHeight);
                    var itemTheme = new ButtonStylePack { Roundness = 0f, BorderLength = 0f };
                    itemTheme.Normal.FillColor = DefaultTheme.Transparent;
                    itemTheme.Hover.FillColor = DefaultTheme.HoverFill;
                    itemTheme.Pressed.FillColor = DefaultTheme.Accent;

                    int itemId = HashCode.Combine(intId, "item", i);

                    if (DrawButtonPrimitive(itemId, itemBounds, items[i], itemTheme, false, new Alignment(HAlignment.Left, VAlignment.Center), DirectUI.Button.ActionMode.Release, DirectUI.Button.ClickBehavior.Left, new Vector2(5, 0), isActive: false, layer: 100) != ClickResult.None)
                    {
                        state.SetPopupResult(intId, i);
                        state.ClearActivePopup();
                    }
                }
            };

            state.SetActivePopup(intId, drawCallback, popupBounds);
        }

        return -1; // No item was clicked on *this* frame
    }


    // --- Helper Methods ---
    private static bool IsContextValid()
    {
        if (UI.Context?.Renderer is null || UI.Context?.TextService is null)
        {
            Console.WriteLine($"Error: UI method called outside BeginFrame/EndFrame or context is invalid.");
            return false;
        }
        return true;
    }
}