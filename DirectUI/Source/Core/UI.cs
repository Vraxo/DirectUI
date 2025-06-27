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

        Context.containerStack.Clear();
        Context.treeStateStack.Clear();
    }

    public static void EndFrame()
    {
        if (Context.containerStack.Count > 0)
        {
            Console.WriteLine($"Warning: Mismatch in Begin/End container calls. {Context.containerStack.Count} containers left open at EndFrame.");
            Context.containerStack.Clear();
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

    private static void ApplyButtonDefinition(Button instance, ButtonDefinition definition)
    {
        instance.Size = definition.Size; instance.Text = definition.Text; instance.Themes = definition.Theme ?? instance.Themes ?? new ButtonStylePack();
        instance.Origin = definition.Origin ?? Vector2.Zero; instance.TextAlignment = definition.TextAlignment ?? new Alignment(HAlignment.Center, VAlignment.Center);
        instance.TextOffset = definition.TextOffset ?? Vector2.Zero; instance.AutoWidth = definition.AutoWidth; instance.TextMargin = definition.TextMargin ?? new Vector2(10, 5);
        instance.Behavior = definition.Behavior; instance.LeftClickActionMode = definition.LeftClickActionMode; instance.Disabled = definition.Disabled; instance.UserData = definition.UserData;
    }

    private static void ApplySliderDefinition(InternalSliderLogic instance, SliderDefinition definition)
    {
        instance.Size = definition.Size; instance.MinValue = definition.MinValue; instance.MaxValue = definition.MaxValue; instance.Step = definition.Step;
        instance.Theme = definition.Theme ?? instance.Theme ?? new SliderStyle(); instance.GrabberTheme = definition.GrabberTheme ?? instance.GrabberTheme ?? new ButtonStylePack();
        instance.GrabberSize = definition.GrabberSize ?? instance.GrabberSize; instance.Origin = definition.Origin ?? Vector2.Zero; instance.Disabled = definition.Disabled; instance.UserData = definition.UserData;
    }
}