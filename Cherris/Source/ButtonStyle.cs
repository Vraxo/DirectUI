using Vortice.DirectWrite;
using Vortice.Mathematics;

namespace Cherris;

public class ButtonStyle : BoxStyle
{
    public Font? Font { get; set; } = null;
    public Color4 FontColor { get; set; } = DefaultTheme.Text;

    public string FontName { get; set; } = "Roboto Mono";
    public float FontSize { get; set; } = 16.0f;
    public FontWeight FontWeight { get; set; } = FontWeight.Normal;
    public FontStyle FontStyle { get; set; } = FontStyle.Normal;
    public FontStretch FontStretch { get; set; } = FontStretch.Normal;
    public WordWrapping WordWrapping { get; set; } = WordWrapping.WholeWord;
}