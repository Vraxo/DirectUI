using DirectUI;
using Raylib_cs;

namespace Cocoshell
{
    public static class ApplicationRunner
    {
        public static void Run(bool useRaylib)
        {
            if (useRaylib)
            {
                RunRaylib();
            }
            else
            {
                RunDirect2D();
            }
        }

        private static void RunRaylib()
        {
            Console.WriteLine("Using Raylib Backend.");
            using var app = new MyDirectUIApp("My Refactored Raylib App", 1024, 768, true);
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
            using var appWindow = new MyDirectUIApp("My Refactored D2D App", 1024, 768, false);
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
}
