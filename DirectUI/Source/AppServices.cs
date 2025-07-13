using DirectUI.Core;

namespace DirectUI;

public sealed class AppServices
{
    public AppEngine AppEngine { get; }
    public DuiGraphicsDevice GraphicsDevice { get; }
    public IRenderer Renderer { get; }
    public ITextService TextService { get; }

    internal AppServices(AppEngine appEngine, DuiGraphicsDevice graphicsDevice, IRenderer renderer, ITextService textService)
    {
        AppEngine = appEngine;
        GraphicsDevice = graphicsDevice;
        Renderer = renderer;
        TextService = textService;
    }
}