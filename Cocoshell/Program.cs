using DirectUI;

namespace Cocoshell;

public class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationRunner.Run(GraphicsBackend.Raylib);
    }
}