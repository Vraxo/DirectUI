using Vortice.Direct2D1;
using Vortice.DirectWrite;

namespace DirectUI;

public readonly struct DrawingContext
{
    public readonly ID2D1HwndRenderTarget RenderTarget;
    public readonly IDWriteFactory DWriteFactory;

    public DrawingContext(ID2D1HwndRenderTarget renderTarget, IDWriteFactory dwriteFactory)
    {
        RenderTarget = renderTarget;
        DWriteFactory = dwriteFactory;
    }
}