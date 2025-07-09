using DirectUI;

namespace Cocoshell;

public class Program
{
    [STAThread]
    static void Main()
    {
        //var backend = GraphicsBackend.Raylib;
        var backend = GraphicsBackend.Direct2D;

        ApplicationRunner.Run(backend);
    }
}