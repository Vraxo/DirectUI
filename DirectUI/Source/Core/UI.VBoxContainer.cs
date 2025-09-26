using System.Numerics;

namespace DirectUI;

public static partial class UI
{
    public static void BeginVBoxContainer(string id, Vector2 position, float gap = 5.0f, Vector2 minSize = default)
    {
        Context.Layout.BeginVBox(id.GetHashCode(), position, gap, minSize);
    }

    public static void EndVBoxContainer()
    {
        if (Context.Layout.ContainerStackCount == 0 || Context.Layout.PeekContainer() is not VBoxContainerState state)
        {
            Console.WriteLine("Error: EndVBoxContainer called without a matching BeginVBoxContainer.");
            return;
        }

        Context.Layout.PopContainer();

        if (!Context.Layout.IsInLayoutContainer())
        {
            return;
        }

        Context.Layout.AdvanceContainerLayout(state.GetAccumulatedSize());
    }
}