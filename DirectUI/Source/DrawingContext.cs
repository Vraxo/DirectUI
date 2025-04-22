// DrawingContext.cs
using Vortice.Direct2D1;
using Vortice.DirectWrite;

namespace DirectUI;

// Holds rendering resources needed for UI drawing
public readonly struct DrawingContext
{
    public readonly ID2D1HwndRenderTarget RenderTarget;
    public readonly IDWriteFactory DWriteFactory;
    // Could potentially add D2DFactory later if needed

    public DrawingContext(ID2D1HwndRenderTarget renderTarget, IDWriteFactory dwriteFactory)
    {
        RenderTarget = renderTarget;
        DWriteFactory = dwriteFactory;
    }
}