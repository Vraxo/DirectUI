using System;
using System.Numerics;

namespace DirectUI;

public static partial class UI
{
    // --- Core Components ---
    public static UIContext Context { get; private set; } = null!;
    public static UIPersistentState State { get; private set; } = null!;
    public static UIResources Resources { get; private set; } = null!;
    public static bool IsRendering { get; private set; } = false;

    // --- Frame Management ---
    public static void BeginFrame(UIContext context)
    {
        IsRendering = true;

        Context = context;
        Resources = context.Resources; // Set the active resources for this frame
        State ??= new UIPersistentState();

        State.ResetFrameState(context.InputState);

        Context.Layout.ClearStack();
        Context.treeStateStack.Clear();
    }

    public static void EndFrame()
    {
        // If a click happened this frame but no UI element captured it, clear the focus.
        if (Context.InputState.WasLeftMousePressedThisFrame && State.InputCaptorId == 0)
        {
            State.SetFocus(0);
        }

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
        Resources = null!;
        IsRendering = false;
    }

    // --- Helper Methods ---
    private static bool IsContextValid()
    {
        if (Context?.RenderTarget is null || Context?.DWriteFactory is null || Resources is null)
        {
            Console.WriteLine($"Error: UI method called outside BeginFrame/EndFrame or context is invalid.");
            return false;
        }
        return true;
    }
}