using DirectUI;

namespace Daw;

public static class Program
{
    [STAThread]
    public static void Main()
    {
        var backend = GraphicsBackend.SkiaSharp;
        ApplicationRunner.Run(backend, host => new DawAppLogic(host));
    }
}