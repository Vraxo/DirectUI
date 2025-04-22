// MODIFIED: HBoxContainerState.cs
// Summary: Added the missing StartPosition property.
using System.Numerics;

namespace DirectUI;

internal class HBoxContainerState
{
    internal string Id { get; }
    internal Vector2 StartPosition { get; } // Added missing property
    internal Vector2 CurrentPosition { get; set; }
    internal float Gap { get; }
    internal float MaxElementHeight { get; set; } = 0f;
    internal float AccumulatedWidth { get; set; } = 0f;

    internal HBoxContainerState(string id, Vector2 startPosition, float gap)
    {
        Id = id;
        StartPosition = startPosition; // Initialize the new property
        CurrentPosition = startPosition;
        Gap = gap;
    }
}