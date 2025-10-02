using System.Numerics;

namespace DirectUI;

public static partial class UI
{
    public static void BeginGridContainer(string id, Vector2 position, Vector2 availableSize, int numColumns, Vector2 gap)
    {
        var startPos = Context.Layout.GetCurrentPosition() + position;
        Context.Layout.PushContainer(new GridContainerState(id.GetHashCode(), startPos, availableSize, numColumns, gap));
    }

    public static void EndGridContainer()
    {
        if (Context.Layout.ContainerStackCount == 0 || Context.Layout.PeekContainer() is not GridContainerState state)
        {
            Console.WriteLine("Error: EndGridContainer called without a matching BeginGridContainer.");
            return;
        }

        Context.Layout.PopContainer();

        if (!Context.Layout.IsInLayoutContainer())
        {
            return;
        }

        Vector2 containerSize = state.GetTotalOccupiedSize();
        Context.Layout.AdvanceContainerLayout(containerSize);
    }
}