using DirectUI;
using DirectUI.Core;

namespace Bankan;

public static class Program
{
    public static void Main()
    {
        ApplicationRunner.Run(GraphicsBackend.SkiaSharp, CreateKanbanApp);
    }

    private static IAppLogic CreateKanbanApp(IWindowHost windowHost)
    {
        return new AppLogic(windowHost);
    }
}