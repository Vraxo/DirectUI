using System.Numerics;

namespace DirectUI;

public class HBoxContainerState : ILayoutContainer
{
    internal int Id { get; }
    public Vector2 StartPosition { get; internal set; }
    internal Vector2 CurrentPosition { get; set; }
    internal float Gap { get; set; }
    internal float MaxElementHeight { get; set; } = 0f;
    internal float AccumulatedWidth { get; set; } = 0f;
    internal int ElementCount { get; set; } = 0;
    internal VAlignment VerticalAlignment { get; set; } = VAlignment.Top;
    internal float? FixedRowHeight { get; set; }
    internal BoxStyle? BackgroundStyle { get; set; }
    internal bool IsBufferingCommands { get; set; }
    public float? ForcedHeight { get; set; }


    internal HBoxContainerState(int id)
    {
        Id = id;
    }

    public Vector2 GetCurrentPosition() => CurrentPosition;

    public void Advance(Vector2 elementSize)
    {
        if (elementSize.Y > MaxElementHeight)
        {
            MaxElementHeight = elementSize.Y;
        }

        AccumulatedWidth += elementSize.X;
        if (ElementCount > 0)
        {
            AccumulatedWidth += Gap;
        }
        float advanceX = elementSize.X + Gap;
        CurrentPosition = new Vector2(CurrentPosition.X + advanceX, CurrentPosition.Y);
        ElementCount++;
    }

    public Vector2 GetAccumulatedSize() => new(AccumulatedWidth, ForcedHeight ?? MaxElementHeight);
}