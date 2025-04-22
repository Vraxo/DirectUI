// ButtonDefinition.cs
using System.Numerics;
using Vortice.Mathematics; // For Color4 if needed directly, though ButtonStylePack handles colors

namespace DirectUI;

public class ButtonDefinition
{
    public Vector2 Position { get; set; } = Vector2.Zero;
    public Vector2 Size { get; set; } = new(84, 28); // Default size
    public string Text { get; set; } = "";
    public ButtonStylePack? Theme { get; set; } = null; // Optional theme override
    public Vector2? Origin { get; set; } = null; // Use null to indicate default (Vector2.Zero)
    public Alignment? TextAlignment { get; set; } = null; // Use null for default (Center, Center)
    public Vector2? TextOffset { get; set; } = null; // Use null for default (Vector2.Zero)
    public bool AutoWidth { get; set; } = false;
    public Vector2? TextMargin { get; set; } = null; // Use null for default (10, 5)
    public Button.ClickBehavior Behavior { get; set; } = Button.ClickBehavior.Left;
    public Button.ActionMode LeftClickActionMode { get; set; } = Button.ActionMode.Release;
    public bool Disabled { get; set; } = false;
    public object? UserData { get; set; } = null;
}