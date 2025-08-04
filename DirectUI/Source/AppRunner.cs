using DirectUI.Backends.SDL3;
using DirectUI.Core; // Added for IWindowHost
using Raylib_cs;
using SDL3;
using GraphicsBackend = DirectUI.GraphicsBackend;

namespace DirectUI;

public static class ApplicationRunner
{
public static void Run(GraphicsBackend backend, Func<IWindowHost, IAppLogic> appLogicFactory)
    {
        IWindowHost? host = null;
        IAppLogic? appLogic = null;
        try
        {
            host = backend switch
            {
                GraphicsBackend.Raylib => new RaylibWindowHost("My Raylib App", 1024, 768, new Vortice.Mathematics.Color4(45 / 255f, 45 / 255f, 45 / 255f, 1.0f)),
                GraphicsBackend.Direct2D => new Win32WindowHost("My D2D App", 1024, 768),
                GraphicsBackend.SDL3 => new SDL3WindowHost("My SDL3 App", 1024, 768, new Vortice.Mathematics.Color4(45 / 255f, 45 / 255f, 45 / 255f, 1.0f)),
                _ => throw new ArgumentOutOfRangeException(nameof(backend), backend, "Unsupported graphics backend.")
            };

            Console.WriteLine($"Using {backend} Backend.");

            appLogic = appLogicFactory(host);

            if (host.Initialize(appLogic.DrawUI, new Vortice.Mathematics.Color4(45 / 255f, 45 / 255f, 45 / 255f, 1.0f))) // #2D2D2D
            {
                host.RunLoop();
            }
            else
            {
                Console.WriteLine($"Failed to initialize {backend} application.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An unhandled exception occurred: {ex}");
        }
        finally
        {
            // Save application state before cleaning up resources.
            if (appLogic is not null)
            {
                Console.WriteLine("Application shutting down. Saving state...");
                appLogic.SaveState();
            }

            host?.Cleanup();
            host?.Dispose();
        }
    }

    // Removed specific RunRaylib, RunDirect2D, RunSDL3 methods
    // as their logic is now encapsulated within their respective IWindowHost implementations.
}
