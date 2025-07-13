using DirectUI;
using Cocoshell; // Use the namespace for ApplicationRunner

public class Program
{
    [STAThread]
    static void Main()
    {
        //var backend = GraphicsBackend.Raylib;
        //var backend = GraphicsBackend.Direct2D;
        //var backend = GraphicsBackend.Vulkan;
        var backend = GraphicsBackend.SDL3; // Set to SDL3 for testing

        ApplicationRunner.Run(backend);
    }
}
