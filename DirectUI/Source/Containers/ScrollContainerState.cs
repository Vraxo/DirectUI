using System.Numerics;

namespace DirectUI;

public class ScrollContainerState
{
    // State managed by the UI system
    internal Vector2 CurrentScrollOffset { get; set; }

    // Per-frame calculated values
    internal int Id { get; set; }
    internal Vector2 Position { get; set; }
    internal Vector2 VisibleSize { get; set; }
    internal Vector2 ContentSize { get; set; }
    internal bool IsHovered { get; set; }
    internal VBoxContainerState ContentVBox { get; set; } = null!;

    // Public parameterless constructor required for GetOrCreateElement
    public ScrollContainerState() { }
}