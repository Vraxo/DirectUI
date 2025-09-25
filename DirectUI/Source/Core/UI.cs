// Entire file content here
using System;
using System.Numerics;

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


    // --- Procedural Animation ---

    /// <summary>
    /// Calculates a value over time based on a trigger and a user-defined animation curve.
    /// This is ideal for complex, non-linear, "keyframe"-style animations.
    /// </summary>
    /// <typeparam name="T">The type of value to animate (e.g., float, Vector2, Color).</typeparam>
    /// <param name="id">A unique string identifier for this animation instance.</param>
    /// <param name="trigger">A boolean that starts the animation when true and resets it when false.</param>
    /// <param name="defaultValue">The value to return when the animation is not active.</param>
    /// <param name="animationCurve">A function that takes the elapsed time in seconds and returns the animated value.</param>
    /// <returns>The calculated value for the current frame.</returns>
    public static T Animate<T>(
        string id,
        bool trigger,
        T defaultValue,
        Func<float, T> animationCurve)
    {
        if (!IsContextValid()) return defaultValue;

        int intId = id.GetHashCode();
        var state = UI.State.AnimationState;
        var currentTime = UI.Context.TotalTime;

        float startTime = state.GetEventTime(intId);

        if (trigger)
        {
            if (startTime < 0)
            {
                // Event just started, record the time.
                startTime = currentTime;
                state.SetEventTime(intId, startTime);
            }

            float elapsedTime = currentTime - startTime;
            return animationCurve(elapsedTime);
        }
        else
        {
            // Event is not active, clear the timer.
            if (startTime >= 0)
            {
                state.ClearEventTime(intId);
            }
            return defaultValue;
        }
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