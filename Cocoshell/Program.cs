using DirectUI;

namespace Cocoshell;

public class Program
{
    [STAThread] // Important for COM (Direct2D) and Win32 APIs
    static void Main(string[] args)
    {
        Console.WriteLine("Starting Refactored Direct2D Application...");

        // Use 'using' to ensure Dispose is called automatically
        using (var appWindow = new MyDirectUIApp("My Refactored D2D App", 1024, 768))
        {
            try
            {
                // Create the main window, which registers itself with the Application manager.
                if (appWindow.Create())
                {
                    // Run the central message loop that processes all windows.
                    Application.Run();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unhandled exception in Run: {ex}");
                // Log exception, show message box, etc.
            }
        } // appWindow.Dispose() is called here, which triggers its window closure

        Console.WriteLine("Application finished.");
        // Console.ReadKey(); // Optional: Keep console open after exit
    }
}