namespace Cocoshell;

public class Program
{
    [STAThread]
    static void Main()
    {
        Console.WriteLine("Starting Refactored Direct2D/Raylib Application...");

        bool useRaylib = false;
        ApplicationRunner.Run(useRaylib);

        Console.WriteLine("Application finished.");
    }
}
