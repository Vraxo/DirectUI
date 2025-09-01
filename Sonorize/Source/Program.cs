using DirectUI;

namespace Sonorize;

public static class Program
{
    [STAThread]
    public static void Main()
    {
        var backend = GraphicsBackend.Direct2D;
        ApplicationRunner.Run(backend, host => new SonorizeLogic(host));
    }
}