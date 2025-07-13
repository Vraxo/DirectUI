using DirectUI;
using DirectUI.Backends.SDL3;
using Raylib_cs;
using SDL3;
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

            case GraphicsBackend.SDL3: // NEW
                RunSDL3();
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(backend), backend, null);
        }
    }

    private static void RunRaylib()
    {
        Console.WriteLine("Using Raylib Backend.");

        using MyDesktopAppWindow appWindow = new("My Refactored Raylib App", 1024, 768, GraphicsBackend.Raylib);

        if (appWindow.CreateHostWindow())
        {
            while (!Raylib.WindowShouldClose())
            {
                appWindow.FrameUpdate();
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

        using MyDesktopAppWindow appWindow = new("My Refactored D2D App", 1024, 768, GraphicsBackend.Direct2D);

        try
        {
            if (appWindow.CreateHostWindow()) // Call the new creation method
            {
                Application.Run();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unhandled exception in D2D Run: {ex}");
        }
    }

    private unsafe static void RunSDL3() // NEW method, similar structure to Vulkan
    {
        Console.WriteLine("Using SDL3 Backend.");

        SDL.Init(SDL.InitFlags.Video);
        TTF.Init(); // Initialize SDL_ttf

        // Create window
        uint width = 1024;
        uint height = 768;
        string title = "My SDL3 App";

        nint window = SDL.CreateWindow(title, (int)width, (int)height, SDL.WindowFlags.Resizable);

        // Create renderer
        nint renderer = SDL.CreateRenderer(window, null);

        MyUILogic appLogic = new(GraphicsBackend.SDL3); // Now instantiate MyUILogic

        appLogic.SetOpenProjectWindowHostAction(() =>
        {
            Console.WriteLine("Modal windows not supported for SDL3 yet.");
        }); // Provide dummy action

        Vortice.Mathematics.Color4 backgroundColor = new(21 / 255f, 21 / 255f, 21 / 255f, 1.0f); // #151515
        SDL3UIHost host = new(appLogic.DrawUI, backgroundColor, renderer, window);

        host.Initialize();

        // SDL.StartTextInput(window); // Removed to prevent potential mouse event interference

        bool running = true;

        while (running)
        {
            while (SDL.PollEvent(out SDL.Event ev))
            {
                host.Input.ProcessSDL3Event(ev);

                if ((SDL.EventType)ev.Type == SDL.EventType.Quit)
                {
                    running = false;
                }
                else if ((SDL.EventType)ev.Type == SDL.EventType.WindowResized || (SDL.EventType)ev.Type == SDL.EventType.WindowPixelSizeChanged)
                {
                    host.Resize(ev.Window.Data1, ev.Window.Data2);
                }
            }

            host.Render();
        }

        // SDL.StopTextInput(window); // Removed

        host.Cleanup();
        SDL.DestroyRenderer(renderer);
        SDL.DestroyWindow(window);
        SDL.Quit();
        TTF.Quit(); // Quit SDL_ttf after all resources are cleaned up
    }
}