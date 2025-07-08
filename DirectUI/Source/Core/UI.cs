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
        if (Context.InputState.WasLeftMousePressedThisFrame)
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