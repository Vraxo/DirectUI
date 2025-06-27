using System.Numerics;

namespace DirectUI;

public class LineEditDefinition
{
    public Vector2 Position { get; set; } = Vector2.Zero;
    public Vector2 Size { get; set; } = new(150, 24);
    public ButtonStylePack? Theme { get; set; } = null;
    public string PlaceholderText { get; set; } = "";
    public bool IsPassword { get; set; } = false;
    public char PasswordChar { get; set; } = '*';
    public int MaxLength { get; set; } = 1024;
    public bool Disabled { get; set; } = false;
    public Vector2 TextMargin { get; set; } = new(4, 2); // Padding inside the control
}