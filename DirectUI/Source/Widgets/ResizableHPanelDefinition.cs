using System.Numerics;

namespace DirectUI;

public class ResizableHPanelDefinition
{
    public float MinHeight { get; set; } = 50f;
    public float MaxHeight { get; set; } = 300f;
    public float ResizeHandleWidth { get; set; } = 5f; // This is actually handle *height* here
    public BoxStyle? PanelStyle { get; set; } = null;
    public Vector2 Padding { get; set; } = new Vector2(5, 5);
    public float Gap { get; set; } = 5f;
    public bool Disabled { get; set; } = false;
}