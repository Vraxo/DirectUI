using DirectUI;

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
                appWindow.Run(); // Creates window, enters message loop
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unhandled exception in Run: {ex}");
                // Log exception, show message box, etc.
            }
        } // appWindow.Dispose() is called here

        Console.WriteLine("Application finished.");
        // Console.ReadKey(); // Optional: Keep console open after exit
    }
}