using System.Numerics;

namespace DirectUI;

public class VBoxContainerState : ILayoutContainer
{
    internal int Id { get; }
    internal Vector2 StartPosition { get; set; }
    internal Vector2 CurrentPosition { get; set; } // Top-left for the next element
    internal float Gap { get; set; }
    internal float MaxElementWidth { get; set; } = 0f; // Track width for container bounds
    internal float AccumulatedHeight { get; set; } = 0f; // Track height for container bounds
    internal int ElementCount { get; set; } = 0;

    internal VBoxContainerState(int id)
    {
        Id = id;
    }

    public Vector2 GetCurrentPosition() => CurrentPosition;

    public void Advance(Vector2 elementSize)
    {
        if (elementSize.X > MaxElementWidth)
        {
            MaxElementWidth = elementSize.X;
        }

        AccumulatedHeight += elementSize.Y;
        if (ElementCount > 0)
        {
            AccumulatedHeight += Gap;
        }
        float advanceY = elementSize.Y + Gap;
        CurrentPosition = new Vector2(CurrentPosition.X, CurrentPosition.Y + advanceY);
        ElementCount++;
    }

    public Vector2 GetAccumulatedSize() => new(MaxElementWidth, AccumulatedHeight);
}