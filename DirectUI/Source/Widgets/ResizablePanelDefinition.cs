using System.Numerics;

namespace DirectUI;

public class ResizablePanelDefinition
{
    public float MinWidth { get; set; } = 50f;
    public float MaxWidth { get; set; } = 500f;
    public float ResizeHandleWidth { get; set; } = 5f;
    public BoxStyle? PanelStyle { get; set; } = null;
    public Vector2 Padding { get; set; } = new Vector2(5, 5);
    public float Gap { get; set; } = 5f;
    public bool Disabled { get; set; } = false;
}