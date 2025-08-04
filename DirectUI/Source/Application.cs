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

            // After processing messages, if no WM_QUIT was posted, allow all registered
            // windows to perform their per-frame updates. This is crucial for logic
            // that needs to run every frame, like checking for modal window closure.
            if (isRunning)
            {
                // Iterate over a copy so we can modify the original list (e.g., on window close).
                // This is safer than iterating over the list directly if FrameUpdate can cause
                // a window to be unregistered.
                foreach (var window in windows.ToList())
                {
                    // FrameUpdate implementations should be safe to call even if the
                    // window's handle has been destroyed in the same frame.
                    window.FrameUpdate();
                }
            }
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