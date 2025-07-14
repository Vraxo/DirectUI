using Vortice.Mathematics;

namespace DirectUI;

internal static class Win32AppServicesInitializer
{
    public static AppServices Initialize(IntPtr hwnd, SizeI clientSize, Action<UIContext> uiDrawCallback, Color4 backgroundColor)
    {
        var appEngine = new AppEngine(uiDrawCallback, backgroundColor);
        var graphicsDevice = new DuiGraphicsDevice();

        if (!graphicsDevice.Initialize(hwnd, clientSize))
        {
            throw new InvalidOperationException("Failed to initialize DuiGraphicsDevice.");
        }

        if (graphicsDevice.RenderTarget is null || graphicsDevice.DWriteFactory is null)
        {
            throw new InvalidOperationException("CRITICAL: GraphicsDevice did not provide valid RenderTarget or DWriteFactory for Direct2D backend initialization.");
        }

        var renderer = new Backends.Direct2DRenderer(graphicsDevice.RenderTarget, graphicsDevice.DWriteFactory, graphicsDevice.D3DDevice, graphicsDevice.D3DContext);
        var textService = new Backends.DirectWriteTextService(graphicsDevice.DWriteFactory);

        appEngine.Initialize(textService, renderer);

        Console.WriteLine($"Win32AppServices Initializer: Services created for HWND {hwnd}.");
        return new AppServices(appEngine, graphicsDevice, renderer, textService);
    }
}
