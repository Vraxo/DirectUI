using DirectUI.Drawing;

namespace DirectUI;

public class KnobStyle
{
    /// <summary>
    /// Style for the main body of the knob.
    /// </summary>
    public BoxStyle BaseStyle { get; set; } = new();

    /// <summary>
    /// Color of the indicator line.
    /// </summary>
    public Color IndicatorColor { get; set; } = DefaultTheme.Accent;

    /// <summary>
    /// Thickness of the indicator line.
    /// </summary>
    public float IndicatorThickness { get; set; } = 2.0f;

    /// <summary>
    /// The start angle of the knob's rotation in degrees. (e.g., -135 for bottom-left start).
    /// </summary>
    public float StartAngleDegrees { get; set; } = -135f;

    /// <summary>
    /// The total range of motion for the knob in degrees. (e.g., 270 for a 3/4 turn).
    /// </summary>
    public float AngleRangeDegrees { get; set; } = 270f;
}
