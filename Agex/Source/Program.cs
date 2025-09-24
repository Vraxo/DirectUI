using DirectUI;

namespace Agex;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        ApplicationRunner.Run(GraphicsBackend.Direct2D, (host) => new AgexApp(host));
    }
}