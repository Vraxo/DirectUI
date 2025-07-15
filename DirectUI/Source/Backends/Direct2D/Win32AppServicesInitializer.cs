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

        if (graphicsDevice.RenderTarget is null || graphicsDevice.DWriteFactory is null || graphicsDevice.D3DDevice is null || graphicsDevice.D3DContext is null || graphicsDevice.SwapChain is null || graphicsDevice.DepthStencilView is null)
        {
            throw new InvalidOperationException("CRITICAL: GraphicsDevice did not provide valid RenderTarget, DWriteFactory, D3DDevice, D3DContext, SwapChain, or DepthStencilView for Direct2D backend initialization.");
        }

        var renderer = new Backends.Direct2DRenderer(graphicsDevice.RenderTarget, graphicsDevice.DWriteFactory, graphicsDevice.D3DDevice, graphicsDevice.D3DContext, graphicsDevice.SwapChain, graphicsDevice.DepthStencilView);
        var textService = new Backends.DirectWriteTextService(graphicsDevice.DWriteFactory);

        appEngine.Initialize(textService, renderer);

        Console.WriteLine($"Win32AppServices Initializer: Services created for HWND {hwnd}.");
        return new AppServices(appEngine, graphicsDevice, renderer, textService);
    }
}