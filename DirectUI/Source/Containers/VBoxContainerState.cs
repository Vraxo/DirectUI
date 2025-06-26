using System.Numerics;

namespace DirectUI;

internal class VBoxContainerState
{
    internal string Id { get; }
    internal Vector2 StartPosition { get; }
    internal Vector2 CurrentPosition { get; set; } // Top-left for the next element
    internal float Gap { get; }
    internal float MaxElementWidth { get; set; } = 0f; // Track width for container bounds
    internal float AccumulatedHeight { get; set; } = 0f; // Track height for container bounds
    internal int ElementCount { get; set; } = 0;

    internal VBoxContainerState(string id, Vector2 startPosition, float gap)
    {
        Id = id;
        StartPosition = startPosition;
        CurrentPosition = startPosition;
        Gap = gap;
    }
}