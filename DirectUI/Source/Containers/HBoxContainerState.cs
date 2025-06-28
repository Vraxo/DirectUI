using System.Numerics;

namespace DirectUI;

public class HBoxContainerState
{
    internal int Id { get; }
    internal Vector2 StartPosition { get; set; }
    internal Vector2 CurrentPosition { get; set; }
    internal float Gap { get; set; }
    internal float MaxElementHeight { get; set; } = 0f;
    internal float AccumulatedWidth { get; set; } = 0f;
    internal int ElementCount { get; set; } = 0;

    internal HBoxContainerState(int id)
    {
        Id = id;
    }
}