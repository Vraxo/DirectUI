namespace DirectUI;

public static class Application
{
    private static readonly List<Win32Window> windows = [];
    private static bool isRunning = false;

    static Application()
    {
        SharedGraphicsResources.Initialize();
    }

    public static void RegisterWindow(Win32Window window)
    {
        if (windows.Contains(window))
        {
            return;
        }

        windows.Add(window);
    }

    public static void UnregisterWindow(Win32Window window)
    {
        windows.Remove(window);

        if (windows.Count != 0)
        {
            return;
        }

        Exit();
    }

    public static void RunMessageLoop()
    {
        if (windows.Count == 0)
        {
            Console.WriteLine("Application.RunMessageLoop() called with no Win32 windows registered.");
            return;
        }

        isRunning = true;

        while (isRunning)
        {
            ProcessMessages();

            // After processing messages, if no WM_QUIT was posted and there are still windows,
            // yield control back to the specific Win32WindowHost for its frame update.
            // Note: In this architecture, the Win32WindowHost's RunLoop will call Application.RunMessageLoop.
            // The FrameUpdate logic should ideally live within the host's own loop.
            // For now, this is a simplified message pump.
        }

        SharedGraphicsResources.Cleanup();
    }

    internal static void ProcessMessages()
    {
        while (NativeMethods.PeekMessage(out var msg, IntPtr.Zero, 0, 0, NativeMethods.PM_REMOVE))
        {
            if (msg.message == NativeMethods.WM_QUIT)
            {
                isRunning = false;
                break;
            }

            NativeMethods.TranslateMessage(ref msg);
            NativeMethods.DispatchMessage(ref msg);
        }
    }

    public static void Exit()
    {
        if (!isRunning)
        {
            return;
        }

        isRunning = false;
        NativeMethods.PostQuitMessage(0);
    }
}