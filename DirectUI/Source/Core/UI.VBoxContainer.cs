using System.Numerics;
using DirectUI.Core;
using Vortice.Mathematics;

namespace DirectUI;

public static partial class UI
{
    public static void BeginVBoxContainer(string id, Vector2 position, float gap = 5.0f, BoxStyle? background = null, float? forcedWidth = null)
    {
        var hash = id.GetHashCode();
        VBoxContainerState vboxState = Context.Layout.GetOrCreateVBoxState(hash);
        vboxState.BackgroundStyle = background;
        vboxState.IsBufferingCommands = background != null;
        vboxState.ForcedWidth = forcedWidth;

        if (vboxState.IsBufferingCommands)
        {
            Context.PushRenderer(new CommandBufferRenderer(Context.Renderer));
        }

        Context.Layout.BeginVBox(hash, position, gap);
    }

    public static void EndVBoxContainer()
    {
        if (Context.Layout.ContainerStackCount == 0 || Context.Layout.PeekContainer() is not VBoxContainerState)
        {
            Console.WriteLine("Error: EndVBoxContainer called without a matching BeginVBoxContainer.");
            return;
        }

        var state = (VBoxContainerState)Context.Layout.PopContainer();
        var accumulatedSize = state.GetAccumulatedSize();

        if (state.IsBufferingCommands && Context.Renderer is CommandBufferRenderer cmdRenderer)
        {
            Context.PopRenderer();

            if (state.BackgroundStyle != null)
            {
                var style = state.BackgroundStyle;
                var pos = state.StartPosition;
                var size = accumulatedSize;
                var scale = Context.UIScale;

                // 1. Draw background with the real renderer
                Context.Renderer.DrawBox(new Rect(pos.X * scale, pos.Y * scale, size.X * scale, size.Y * scale), style);
            }

            // 2. Replay content draws on top of the background
            cmdRenderer.Replay(Context.Renderer);
        }

        if (!Context.Layout.IsInLayoutContainer())
        {
            return;
        }

        Context.Layout.AdvanceContainerLayout(accumulatedSize);
    }
}