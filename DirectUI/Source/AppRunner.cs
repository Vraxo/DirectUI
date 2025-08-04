using DirectUI.Backends.SDL3;
using DirectUI.Core; // Added for IWindowHost
using Raylib_cs;
using SDL3;
using GraphicsBackend = DirectUI.GraphicsBackend;

namespace DirectUI;

public static class ApplicationRunner
{
    private static bool _isSavedOnExit = false;

    public static void Run(GraphicsBackend backend, Func<IWindowHost, IAppLogic> appLogicFactory)
    {
        IWindowHost? host = null;
        IAppLogic? appLogic = null;

        // Define a guarded save action to prevent saving more than once on exit.
        Action? saveAction = null;

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

            saveAction = () =>
            {
                if (_isSavedOnExit || appLogic == null) return;

                Console.WriteLine("Exit detected. Saving application state...");
                appLogic.SaveState();
                _isSavedOnExit = true;
            };

            // Hook into the AppDomain.ProcessExit event. This is a more reliable way
            // to catch process termination, including closing the console window.
            AppDomain.CurrentDomain.ProcessExit += (sender, e) =>
            {
                saveAction?.Invoke();
            };

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
            // The finally block is executed on a clean shutdown (e.g., closing the GUI window).
            // We call the same guarded save action here.
            saveAction?.Invoke();

            host?.Cleanup();
            host?.Dispose();
        }
    }

    // Removed specific RunRaylib, RunDirect2D, RunSDL3 methods
    // as their logic is now encapsulated within their respective IWindowHost implementations.
}