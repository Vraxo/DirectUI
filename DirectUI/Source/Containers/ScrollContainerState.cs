using System.Numerics;

namespace DirectUI;

public class ScrollContainerState : ILayoutContainer
{
    // State managed by the UI system
    internal Vector2 CurrentScrollOffset { get; set; }

    // Per-frame calculated values
    internal int Id { get; set; }
    internal Vector2 Position { get; set; }
    internal Vector2 VisibleSize { get; set; }
    internal Vector2 ContentSize { get; set; } // Content size from *previous* frame
    internal bool IsHovered { get; set; }

    // State for the current frame's layout pass
    internal Vector2 CalculatedContentSize { get; set; } // Content size measured *this* frame
    private Vector2 ContentStartPosition { get; set; }

    public ScrollContainerState() { }

    // This is called by child containers to get their starting position.
    public Vector2 GetCurrentPosition() => ContentStartPosition;

    // This is called by the child container when it finishes, reporting its total size.
    public void Advance(Vector2 elementSize)
    {
        CalculatedContentSize = Vector2.Max(CalculatedContentSize, elementSize);
    }

    // Internal method for BeginScrollableRegion to set up the content's drawing position.
    internal void SetContentStartPosition(Vector2 position)
    {
        ContentStartPosition = position;
    }
}