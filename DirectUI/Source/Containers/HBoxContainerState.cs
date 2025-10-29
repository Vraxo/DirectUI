using System.Numerics;

namespace DirectUI;

public class HBoxContainerState : ILayoutContainer
{
    internal int Id { get; }
    internal Vector2 StartPosition { get; set; }
    internal Vector2 CurrentPosition { get; set; }
    internal float Gap { get; set; }
    internal float MaxElementHeight { get; set; } = 0f;
    internal float AccumulatedWidth { get; set; } = 0f;
    internal int ElementCount { get; set; } = 0;
    internal VAlignment VerticalAlignment { get; set; } = VAlignment.Top;
    internal float? FixedRowHeight { get; set; }

    internal HBoxContainerState(int id)
    {
        Id = id;
    }

    public Vector2 GetCurrentPosition() => CurrentPosition;

    public void Advance(Vector2 elementSize)
    {
        // If this is not the first element, add a gap BEFORE it.
        if (ElementCount > 0)
        {
            CurrentPosition = new Vector2(CurrentPosition.X + Gap, CurrentPosition.Y);
            AccumulatedWidth += Gap;
        }

        // Now, account for the element itself.
        if (elementSize.Y > MaxElementHeight)
        {
            MaxElementHeight = elementSize.Y;
        }

        AccumulatedWidth += elementSize.X;

        // The next logical position is simply after the current element.
        // The gap for the *next* element will be handled in its own Advance call.
        CurrentPosition = new Vector2(CurrentPosition.X + elementSize.X, CurrentPosition.Y);
        ElementCount++;
    }
}