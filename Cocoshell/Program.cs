using DirectUI;

namespace Cocoshell;

public class Program
{
    [STAThread]
    static void Main()
    {
        //var backend = GraphicsBackend.Raylib;
        //var backend = GraphicsBackend.Direct2D;
        var backend = GraphicsBackend.Vulkan;

        ApplicationRunner.Run(backend);
    }
}