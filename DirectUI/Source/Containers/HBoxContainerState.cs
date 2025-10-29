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
        // Update max height for the row
        if (elementSize.Y > MaxElementHeight)
        {
            MaxElementHeight = elementSize.Y;
        }

        // Update total width for container bounds.
        // The gap is added before each element except the first.
        if (ElementCount > 0)
        {
            AccumulatedWidth += Gap;
        }
        AccumulatedWidth += elementSize.X;

        // Calculate the advance amount, including the gap *after* the current element,
        // to correctly position the cursor for the start of the *next* element.
        float advanceX = elementSize.X + Gap;
        CurrentPosition = new Vector2(CurrentPosition.X + advanceX, CurrentPosition.Y);

        ElementCount++;
    }
}