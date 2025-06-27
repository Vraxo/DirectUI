using System;
using System.Collections.Generic;

namespace DirectUI;

/// <summary>
/// Manages the application's global message loop and window collection.
/// </summary>
public static class Application
{
    private static readonly List<Win32Window> s_windows = new();
    private static bool s_isRunning = false;

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
        // If the last window is closed, exit the application.
        if (s_windows.Count == 0)
        {
            Exit();
        }
    }

    /// <summary>
    /// Starts and runs the main application message loop.
    /// </summary>
    public static void Run()
    {
        if (s_windows.Count == 0)
        {
            Console.WriteLine("Application.Run() called with no windows registered.");
            return;
        }

        s_isRunning = true;
        while (s_isRunning)
        {
            ProcessMessages();

            if (!s_isRunning) break;

            // Create a copy for safe iteration, as windows can be closed (and removed) during the loop.
            var windowsToUpdate = new List<Win32Window>(s_windows);
            foreach (var window in windowsToUpdate)
            {
                if (window.Handle != IntPtr.Zero)
                {
                    window.FrameUpdate();
                }
            }
        }
    }

    /// <summary>
    /// Processes all pending window messages in the queue.
    /// </summary>
    private static void ProcessMessages()
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
    /// Signals the application to exit its message loop.
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