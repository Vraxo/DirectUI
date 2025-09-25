using DirectUI;
using DirectUI.Core;

namespace Bankan;

public static class Program
{
    public static void Main(string[] args)
    {
        // The ApplicationRunner will create the correct window host based on the backend.
        // We'll use Direct2D as it provides a good look and feel for this kind of app.
        ApplicationRunner.Run(GraphicsBackend.SkiaSharp, CreateKanbanApp);
    }

    private static IAppLogic CreateKanbanApp(IWindowHost windowHost)
    {
        // The window host is passed to our app logic so it can access
        // services like opening modal windows.
        return new KanbanAppLogic(windowHost);
    }
}