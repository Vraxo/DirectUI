using System.Numerics;

namespace DirectUI;

internal class HBoxContainerState
{
    internal string Id { get; }
    internal Vector2 StartPosition { get; }
    internal Vector2 CurrentPosition { get; set; }
    internal float Gap { get; }
    internal float MaxElementHeight { get; set; } = 0f;
    internal float AccumulatedWidth { get; set; } = 0f;
    internal int ElementCount { get; set; } = 0;

    internal HBoxContainerState(string id, Vector2 startPosition, float gap)
    {
        Id = id;
        StartPosition = startPosition;
        CurrentPosition = startPosition;
        Gap = gap;
    }
}