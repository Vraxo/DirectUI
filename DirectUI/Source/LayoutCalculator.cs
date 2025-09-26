using System.Numerics;
using DirectUI.Core;

namespace DirectUI;

public class LayoutCalculator
{
    public Vector2 Size => _vbox.GetAccumulatedSize();

    private readonly UIContext _context;
    private readonly VBoxContainerState _vbox;

    public LayoutCalculator(float gap = 0)
    {
        _context = UI.Context;
        _vbox = new VBoxContainerState(0) { Gap = gap };
    }

    public void Add(Vector2 logicalSize)
    {
        _vbox.Advance(logicalSize);
    }

    public void AddWrappedText(string text, float logicalMaxWidth, ButtonStyle? style = null)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        ButtonStyle finalStyle = style ?? new();

        ButtonStyle physicalStyle = new(finalStyle) 
        { 
            FontSize = finalStyle.FontSize * _context.UIScale 
        };

        ITextLayout layout = _context.TextService.GetTextLayout(
            text,
            physicalStyle,
            new(logicalMaxWidth * _context.UIScale, float.MaxValue),
            new(HAlignment.Left, VAlignment.Top)
        );

        Vector2 logicalSize = new(logicalMaxWidth, layout.Size.Y / _context.UIScale);
        
        _vbox.Advance(logicalSize);
    }

    public void AddSeparator(float logicalWidth, float thickness = 1f, float verticalPadding = 4f)
    {
        float logicalTotalHeight = thickness + (verticalPadding * 2);
        
        _vbox.Advance(new(logicalWidth, logicalTotalHeight));
    }
}