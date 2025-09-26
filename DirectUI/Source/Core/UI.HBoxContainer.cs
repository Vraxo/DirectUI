using System.Numerics;

namespace DirectUI;

public static partial class UI
{
    public static void BeginHBoxContainer(string id, Vector2 position, float gap = 5.0f, VAlignment verticalAlignment = VAlignment.Top, float? fixedRowHeight = null)
    {
        HBoxContainerState hboxState = Context.Layout.GetOrCreateHBoxState(id.GetHashCode());
        hboxState.VerticalAlignment = verticalAlignment;
        hboxState.FixedRowHeight = fixedRowHeight;

        Context.Layout.BeginHBox(id.GetHashCode(), position, gap);
    }

    public static void EndHBoxContainer()
    {
        if (Context.Layout.ContainerStackCount == 0 || Context.Layout.PeekContainer() is not HBoxContainerState state)
        {
            Console.WriteLine("Error: EndHBoxContainer called without a matching BeginHBoxContainer.");
            return;
        }

        Context.Layout.PopContainer();

        if (!Context.Layout.IsInLayoutContainer())
        {
            return;
        }

        Context.Layout.AdvanceContainerLayout(new(state.AccumulatedWidth, state.MaxElementHeight));
    }
}