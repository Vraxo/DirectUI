using DirectUI;
using Raylib_cs;

namespace Cocoshell;

public static class ApplicationRunner
{
    public static void Run(GraphicsBackend backend)
    {
        switch (backend)
        {
            case GraphicsBackend.Raylib:
                RunRaylib();
                break;
            case GraphicsBackend.Direct2D:
                RunDirect2D();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(backend), backend, null);
        }
    }

    private static void RunRaylib()
    {
        Console.WriteLine("Using Raylib Backend.");
        
        using MyDirectUIApp app = new("My Refactored Raylib App", 1024, 768, GraphicsBackend.Raylib);
        
        if (app.Create())
        {
            while (!Raylib.WindowShouldClose())
            {
                app.FrameUpdate();
            }
        }
        else
        {
            Console.WriteLine("Failed to initialize Raylib application.");
        }
    }

    private static void RunDirect2D()
    {
        Console.WriteLine("Using Direct2D Backend.");

        using MyDirectUIApp appWindow = new("My Refactored D2D App", 1024, 768, GraphicsBackend.Direct2D);
        
        try
        {
            if (appWindow.Create())
            {
                Application.Run();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unhandled exception in D2D Run: {ex}");
        }
    }
}