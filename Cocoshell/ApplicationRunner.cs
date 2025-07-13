using DirectUI;
using DirectUI.Backends.Vulkan;
using Raylib_cs;
using Veldrid;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;
using GraphicsBackend = DirectUI.GraphicsBackend;

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
            case GraphicsBackend.Vulkan:
                RunVulkan();
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

    private static void RunVulkan()
    {
        Console.WriteLine("Using Vulkan (Veldrid) Backend.");
        
        WindowCreateInfo windowCI = new()
        {
            X = 100,
            Y = 100,
            WindowWidth = 1024,
            WindowHeight = 768,
            WindowTitle = "My Veldrid App"
        };

        Sdl2Window window = VeldridStartup.CreateWindow(ref windowCI);

        GraphicsDeviceOptions options = new(false, null, true, ResourceBindingModel.Improved, true, true);
        Veldrid.GraphicsDevice gd = VeldridStartup.CreateGraphicsDevice(window, options, Veldrid.GraphicsBackend.Vulkan);

        using MyDirectUIApp appLogic = new("Veldrid App Logic", 1024, 768, GraphicsBackend.Vulkan);

        Vortice.Mathematics.Color4 backgroundColor = new(21 / 255f, 21 / 255f, 21 / 255f, 1.0f); // #151515
        VeldridUIHost host = new(appLogic.DrawUI, backgroundColor, gd);
        
        host.Initialize();

        window.Resized += () => host.Resize((uint)window.Width, (uint)window.Height);

        while (window.Exists)
        {
            InputSnapshot snapshot = window.PumpEvents();
            
            if (!window.Exists)
            {
                break;
            }

            host.Input.ProcessVeldridInput(snapshot);

            foreach (KeyEvent keyEvent in snapshot.KeyEvents)
            {
                if (keyEvent.Key == Veldrid.Key.F3 && keyEvent.Down)
                {
                    host.ShowFpsCounter = !host.ShowFpsCounter;
                }
            }

            host.Render();
        }

        host.Cleanup();
        gd.Dispose();
        window.Close();
    }
}