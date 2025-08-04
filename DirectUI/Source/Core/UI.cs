using System;
using System.Numerics;

namespace DirectUI;

public static partial class UI
{
    // --- Core Components ---
    public static UIContext Context { get; private set; } = null!;
    public static UIPersistentState State { get; private set; } = null!;
    public static bool IsRendering
    {
        get; private set;
    } = false;

    // --- Frame Management ---
    public static void BeginFrame(UIContext context)
    {
        IsRendering = true;

        Context = context;
        State ??= new UIPersistentState();

        State.ResetFrameState(context.InputState);

        Context.Layout.ClearStack();
        Context.treeStateStack.Clear();
    }

    public static void EndFrame()
    {
        // If a click happened this frame but no UI element captured it, and no popup was open, clear focus.
        if (Context.InputState.WasLeftMousePressedThisFrame && State.InputCaptorId == 0 && !State.IsPopupOpen)
        {
            State.SetFocus(0);
        }

        HandlePopupLogic();

        if (Context.Layout.ContainerStackCount > 0)
        {
            Console.WriteLine($"Warning: Mismatch in Begin/End container calls. {Context.Layout.ContainerStackCount} containers left open at EndFrame.");
            Context.Layout.ClearStack();
        }
        if (Context.treeStateStack.Count > 0)
        {
            Console.WriteLine($"Warning: Mismatch in Begin/End Tree calls. {Context.treeStateStack.Count} trees left open at EndFrame.");
            Context.treeStateStack.Clear();
        }

        // It's important that IsRendering is set to false AFTER the context is cleared.
        Context = null!;
        IsRendering = false;
    }

    /// <summary>
    /// Handles popup logic at the end of a frame, ensuring they are drawn last and closed correctly.
    /// </summary>
    private static void HandlePopupLogic()
    {
        if (!State.IsPopupOpen) return;

        // If a mouse press occurred outside the popup's bounds, close the popup.
        if (Context.InputState.WasLeftMousePressedThisFrame || Context.InputState.WasRightMousePressedThisFrame)
        {
            if (!State.PopupBounds.Contains(Context.InputState.MousePosition))
            {
                State.ClearActivePopup();
                return; // Don't draw the popup since we just closed it.
            }
        }

        // Execute the callback to draw the popup content.
        State.PopupDrawCallback?.Invoke(Context);
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
        var state = State;
        var input = Context.InputState;

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

        var context = Context;
        var state = State;
        int intId = popupId.GetHashCode();
        int clickedItemIndex = -1;

        // If a result for this menu is already available from the previous frame, consume and return it.
        if (state.PopupResultAvailable && state.PopupResultOwnerId == intId)
        {
            return state.PopupResult;
        }

        // Calculate popup properties
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

                if (DrawButtonPrimitive(
                    itemId,
                    itemBounds,
                    items[i],
                    itemTheme,
                    false,
                    new Alignment(HAlignment.Left, VAlignment.Center),
                    DirectUI.Button.ActionMode.Release,
                    DirectUI.Button.ClickBehavior.Left,
                    new Vector2(5, 0)))
                {
                    state.SetPopupResult(intId, i);
                    state.ClearActivePopup();
                }
            }
        };

        state.SetActivePopup(intId, drawCallback, popupBounds);

        return clickedItemIndex;
    }


    // --- Helper Methods ---
    private static bool IsContextValid()
    {
        if (Context?.Renderer is null || Context?.TextService is null)
        {
            Console.WriteLine($"Error: UI method called outside BeginFrame/EndFrame or context is invalid.");
            return false;
        }
        return true;
    }
}