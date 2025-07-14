using DirectUI;

namespace Cocoshell;

public class Program
{
    [STAThread]
    static void Main()
    {
        //var backend = GraphicsBackend.Raylib;
        var backend = GraphicsBackend.Direct2D;
        //var backend = GraphicsBackend.SDL3; // Set to SDL3 for testing

        ApplicationRunner.Run(backend, host => new MyUILogic(host.ModalWindowService));
    }
}
