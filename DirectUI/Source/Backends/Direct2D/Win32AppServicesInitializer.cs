using Vortice.Mathematics;

namespace DirectUI;

public static class Win32AppServicesInitializer
{
    public static AppServices Initialize(IntPtr hwnd, SizeI clientSize, Action<UIContext> uiDrawCallback, Color4 backgroundColor)
    {
        var appEngine = new AppEngine(uiDrawCallback, backgroundColor);
        var graphicsDevice = new DuiGraphicsDevice();

        if (!graphicsDevice.Initialize(hwnd, clientSize))
        {
            throw new InvalidOperationException("Failed to initialize DuiGraphicsDevice.");
        }

        if (graphicsDevice.DWriteFactory is null)
        {
            throw new InvalidOperationException("CRITICAL: GraphicsDevice did not provide a valid DWriteFactory for TextService initialization.");
        }

        // Pass the entire graphics device to the renderer. This ensures the renderer
        // always has access to the current, valid render target, even after a resize.
        var renderer = new Backends.Direct2DRenderer(graphicsDevice);
        var textService = new Backends.DirectWriteTextService(graphicsDevice.DWriteFactory);

        appEngine.Initialize(textService, renderer);

        Console.WriteLine($"Win32AppServices Initializer: Services created for HWND {hwnd}.");
        return new AppServices(appEngine, graphicsDevice, renderer, textService);
    }
}