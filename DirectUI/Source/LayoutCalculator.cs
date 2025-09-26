// DirectUI/Source/LayoutCalculator.cs
using System.Numerics;
using DirectUI.Core;

namespace DirectUI;

/// <summary>
/// A helper class to perform layout calculations ("dry run") without rendering,
/// allowing UI elements to determine the size of their content before drawing it.
/// This is useful for containers that need to draw a background sized to their content.
/// </summary>
public class LayoutCalculator
{
    private readonly UIContext _context;
    private readonly VBoxContainerState _vbox;

    /// <summary>
    /// Initializes a new instance of the LayoutCalculator for a vertical layout.
    /// </summary>
    /// <param name="gap">The logical gap between elements.</param>
    public LayoutCalculator(float gap = 0)
    {
        _context = UI.Context;
        _vbox = new VBoxContainerState(0) { Gap = gap };
    }

    /// <summary>
    /// Adds an item with a fixed logical size to the layout calculation.
    /// </summary>
    public void Add(Vector2 logicalSize)
    {
        _vbox.Advance(logicalSize);
    }

    /// <summary>
    /// Calculates the size of wrapped text and adds it to the layout.
    /// </summary>
    public void AddWrappedText(string text, float logicalMaxWidth, ButtonStyle? style = null)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var finalStyle = style ?? new ButtonStyle();
        var physicalStyle = new ButtonStyle(finalStyle) { FontSize = finalStyle.FontSize * _context.UIScale };

        // Use the TextService to get a properly wrapped text layout
        var layout = _context.TextService.GetTextLayout(
            text,
            physicalStyle,
            new Vector2(logicalMaxWidth * _context.UIScale, float.MaxValue),
            // Alignment doesn't affect the measured size of a wrapped layout
            new Alignment(HAlignment.Left, VAlignment.Top)
        );

        // The layout size is physical, so unscale it to get the logical height for our calculation.
        // The width is constrained by logicalMaxWidth.
        Vector2 logicalSize = new Vector2(logicalMaxWidth, layout.Size.Y / _context.UIScale);
        _vbox.Advance(logicalSize);
    }

    /// <summary>
    /// Adds a separator with standard sizing to the layout calculation.
    /// </summary>
    public void AddSeparator(float logicalWidth, float thickness = 1f, float verticalPadding = 4f)
    {
        float logicalTotalHeight = thickness + (verticalPadding * 2);
        _vbox.Advance(new Vector2(logicalWidth, logicalTotalHeight));
    }

    /// <summary>
    /// Gets the total accumulated size of all items added to the calculator.
    /// </summary>
    /// <returns>The total logical size.</returns>
    public Vector2 GetSize() => _vbox.GetAccumulatedSize();
}