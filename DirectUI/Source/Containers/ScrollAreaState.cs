using System.Numerics;

namespace DirectUI;

/// <summary>
/// A state object for the 2D ScrollArea container.
/// </summary>
public class ScrollAreaState : ILayoutContainer
{
    // --- State managed by the UI system ---
    internal Vector2 CurrentScrollOffset { get; set; } // Stored in logical units

    // --- Per-frame calculated/cached values ---
    internal int Id { get; set; }
    internal Vector2 Position { get; set; }      // Top-left of the entire control (logical)
    internal Vector2 VisibleSize { get; set; }   // The size of the viewport (logical)
    internal Vector2 ContentSize { get; set; }     // The full content size from the *previous* frame (logical)
    internal bool IsHovered { get; set; }

    // --- State for the current frame's layout pass ---
    internal Vector2 CalculatedContentSize { get; set; } // The content size measured *this* frame (logical)
    private Vector2 ContentStartPosition { get; set; }   // The top-left where content should begin drawing (logical)

    public ScrollAreaState() { }

    // This is called by the UILayoutManager to get the starting position for child elements.
    public Vector2 GetCurrentPosition() => ContentStartPosition;

    // This is called by a child container (like HBox) when it finishes laying out its elements.
    // The 'elementSize' is the total size of that child container.
    public void Advance(Vector2 elementSize)
    {
        // A scroll area should ideally only have one direct child container.
        // We take the max in case of weird usage, but it's the size of that single child.
        CalculatedContentSize = Vector2.Max(CalculatedContentSize, elementSize);
    }

    // Internal method for BeginScrollArea to set up the content's drawing position.
    internal void SetContentStartPosition(Vector2 position)
    {
        ContentStartPosition = position;
    }
}