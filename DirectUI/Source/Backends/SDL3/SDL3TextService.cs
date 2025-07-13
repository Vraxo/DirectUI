using System.Numerics;
using DirectUI.Core;

namespace DirectUI.Backends.SDL3;

public class SDL3TextService : ITextService
{
    public SDL3TextService()
    {
        // TTF_Init() will be called here in a later step.
    }

    public Vector2 MeasureText(string text, ButtonStyle style)
    {
        // Placeholder: return a fixed size for now
        return new Vector2(text.Length * style.FontSize * 0.5f, style.FontSize);
    }

    public ITextLayout GetTextLayout(string text, ButtonStyle style, Vector2 maxSize, Alignment alignment)
    {
        // Placeholder: return a dummy layout object
        return new SDL3TextLayout(text, MeasureText(text, style));
    }

    public void Cleanup()
    {
        // TTF_Quit() will be called here in a later step.
    }
}