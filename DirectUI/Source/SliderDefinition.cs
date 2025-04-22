// NEW: SliderDefinition.cs
// Summary: Configuration object for creating immediate-mode sliders (HSlider/VSlider).
using System.Numerics;

namespace DirectUI;

// Enums for direction (can reuse or redefine if needed, assume reuse for now)
// public enum HSliderDirection { LeftToRight, RightToLeft } // Already exists if Cherris files were added
// public enum VSliderDirection { TopToBottom, BottomToTop } // Already exists if Cherris files were added


public class SliderDefinition
{
    public Vector2 Position { get; set; } = Vector2.Zero; // Used if not in a container
    public Vector2 Size { get; set; } = new(200, 16); // Default size (adjust as needed)
    public float MinValue { get; set; } = 0.0f;
    public float MaxValue { get; set; } = 1.0f;
    public float Step { get; set; } = 0.01f;

    public SliderStyle? Theme { get; set; } = null; // Optional override for track style
    public ButtonStylePack? GrabberTheme { get; set; } = null; // Optional override for grabber style
    public Vector2? GrabberSize { get; set; } = null; // Optional override for grabber size (e.g., new(16, 16))

    // Direction specific to slider type, ignored by the other type
    public HSliderDirection HorizontalDirection { get; set; } = HSliderDirection.LeftToRight;
    public VSliderDirection VerticalDirection { get; set; } = VSliderDirection.TopToBottom;

    public bool Disabled { get; set; } = false;
    public object? UserData { get; set; } = null;
    public Vector2? Origin { get; set; } = null; // Use null for default (Vector2.Zero)
}