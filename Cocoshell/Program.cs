using DirectUI;
using Raylib_cs; // Added for Raylib

namespace Cocoshell;

public class Program
{
    [STAThread] // Important for COM (Direct2D) and Win32 APIs
    static void Main(string[] args)
    {
        Console.WriteLine("Starting Refactored Direct2D/Raylib Application...");

        bool useRaylib = false; // Set to true to test Raylib backend

        if (useRaylib)
        {
            Console.WriteLine("Using Raylib Backend.");

            // For Raylib, the main loop is driven directly by Program.cs
            // and Raylib-cs's window management.
            // MyDirectUIApp (when configured for Raylib) will handle Raylib window creation and input.
            using MyDirectUIApp app = new("My Refactored Raylib App", 1024, 768, useRaylib);
            // app.Create() will internally call the correct Initialize for Raylib.
            if (app.Create())
            {
                // The primary application loop for Raylib.
                // This loop runs as long as the Raylib window should not close.
                while (!Raylib.WindowShouldClose())
                {
                    // MyDirectUIApp.FrameUpdate for Raylib backend
                    // handles input processing and calls AppHost.Render().
                    // It also sets Application.s_isRunning to false if Raylib.WindowShouldClose() is true.
                    app.FrameUpdate();
                }
            }
            else
            {
                Console.WriteLine("Failed to initialize Raylib application.");
            }
            // app.Dispose() is called here implicitly, triggering cleanup
        }
        else
        {
            Console.WriteLine("Using Direct2D Backend.");

            // Original Direct2D flow, using Win32 window and message loop.
            using MyDirectUIApp appWindow = new("My Refactored D2D App", 1024, 768, useRaylib);
            
            try
            {
                // Create the main window, which registers itself with the Application manager.
                if (appWindow.Create())
                {
                    // Run the central Win32 message loop that processes all windows.
                    Application.Run();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unhandled exception in D2D Run: {ex}");
            }
        }

        Console.WriteLine("Application finished.");
    }
}
