using System.Numerics;
using DirectUI.Core;
using Vortice.Mathematics;

namespace DirectUI;

public static partial class UI
{
    public static void BeginHBoxContainer(string id, Vector2 position, float gap = 5.0f, VAlignment verticalAlignment = VAlignment.Top, float? fixedRowHeight = null, BoxStyle? background = null, float? forcedHeight = null)
    {
        var hash = id.GetHashCode();
        HBoxContainerState hboxState = Context.Layout.GetOrCreateHBoxState(hash);
        hboxState.VerticalAlignment = verticalAlignment;
        hboxState.FixedRowHeight = fixedRowHeight;
        hboxState.BackgroundStyle = background;
        hboxState.IsBufferingCommands = background != null;
        hboxState.ForcedHeight = forcedHeight;

        if (hboxState.IsBufferingCommands)
        {
            Context.PushRenderer(new CommandBufferRenderer(Context.Renderer));
        }

        Context.Layout.BeginHBox(hash, position, gap);
    }

    public static void EndHBoxContainer()
    {
        if (Context.Layout.ContainerStackCount == 0 || Context.Layout.PeekContainer() is not HBoxContainerState)
        {
            Console.WriteLine("Error: EndHBoxContainer called without a matching BeginHBoxContainer.");
            return;
        }

        var state = (HBoxContainerState)Context.Layout.PopContainer();
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