using System;
using System.Collections.Generic;

namespace DirectUI;

/// <summary>
/// Manages Win32-specific application concerns, such as window registration and the message pump.
/// </summary>
public static class Application
{
    private static readonly List<Win32Window> s_windows = new();
    private static bool s_isRunning = false;

    /// <summary>
    /// A static constructor is guaranteed to run once before the class is used.
    /// This is the perfect place to initialize application-wide resources.
    /// </summary>
    static Application()
    {
        SharedGraphicsResources.Initialize();
    }

    /// <summary>
    /// Registers a window with the application manager. Called by Win32Window's constructor.
    /// </summary>
    public static void RegisterWindow(Win32Window window)
    {
        if (!s_windows.Contains(window))
        {
            s_windows.Add(window);
        }
    }

    /// <summary>
    /// Unregisters a window. Called when a window is destroyed.
    /// </summary>
    public static void UnregisterWindow(Win32Window window)
    {
        s_windows.Remove(window);
        // If the last Win32 window is closed, signal the message loop to exit.
        if (s_windows.Count == 0)
        {
            Exit();
        }
    }

    /// <summary>
    /// Runs the Win32 main message loop. This method blocks until WM_QUIT is received.
    /// This is intended for Win32-based applications. Other backends will have their own loops.
    /// </summary>
    public static void RunMessageLoop()
    {
        if (s_windows.Count == 0)
        {
            Console.WriteLine("Application.RunMessageLoop() called with no Win32 windows registered.");
            return;
        }

        s_isRunning = true;
        while (s_isRunning)
        {
            ProcessMessages();

            // After processing messages, if no WM_QUIT was posted and there are still windows,
            // yield control back to the specific Win32WindowHost for its frame update.
            // Note: In this architecture, the Win32WindowHost's RunLoop will call Application.RunMessageLoop.
            // The FrameUpdate logic should ideally live within the host's own loop.
            // For now, this is a simplified message pump.
        }

        // Clean up shared resources only after the very last Win32 window has closed.
        SharedGraphicsResources.Cleanup();
    }

    /// <summary>
    /// Processes all pending window messages in the queue.
    /// </summary>
    internal static void ProcessMessages()
    {
        while (NativeMethods.PeekMessage(out var msg, IntPtr.Zero, 0, 0, NativeMethods.PM_REMOVE))
        {
            if (msg.message == NativeMethods.WM_QUIT)
            {
                s_isRunning = false;
                break;
            }

            NativeMethods.TranslateMessage(ref msg);
            NativeMethods.DispatchMessage(ref msg);
        }
    }

    /// <summary>
    /// Signals the Win32 message loop to exit.
    /// </summary>
    public static void Exit()
    {
        if (s_isRunning)
        {
            s_isRunning = false;
            // Post a quit message to ensure the loop breaks out of GetMessage if it's blocking.
            NativeMethods.PostQuitMessage(0);
        }
    }
}
