// Entire file content here
using DirectUI.Core;
using DirectUI.Input;
using Vortice.Mathematics;
using SizeI = Vortice.Mathematics.SizeI;

namespace DirectUI;

public class Win32WindowHost : Win32Window, IWindowHost, IModalWindowService
{
    private AppServices? appServices;

    private ModalWindow? activeModalWindow;
    private int modalResultCode;
    private Action<int>? onModalClosedCallback;
    private bool _isModalClosing;
    private readonly System.Diagnostics.Stopwatch _throttleTimer = new();
    private long _lastModalRepaintTicks;
    private static readonly long _modalRepaintIntervalTicks = System.Diagnostics.Stopwatch.Frequency / 10;
    private bool _isCtrlDown; // For zoom

    // --- Logic moved from Application.cs ---
    private static readonly List<Win32Window> s_windows = [];
    private static bool s_isRunning = false;

    public bool IsModalWindowOpen
    {
        get
        {
            return activeModalWindow is not null
                && activeModalWindow.Handle != IntPtr.Zero;
        }
    }

    public Win32WindowHost(string title = "DirectUI Win32 Host", int width = 800, int height = 600)
        : base(title, width, height)
    {
        _throttleTimer.Start();
    }

    public AppEngine AppEngine => appServices?.AppEngine ?? throw new InvalidOperationException("AppEngine is not initialized.");
    public InputManager Input => appServices?.AppEngine.Input ?? new();
    public SizeI ClientSize => GetClientRectSize();

    public bool ShowFpsCounter
    {
        get
        {
            return appServices?.AppEngine.ShowFpsCounter ?? false;
        }

        set
        {
            if (appServices is null)
            {
                return;
            }

            appServices.AppEngine.ShowFpsCounter = value;
        }
    }

    public IModalWindowService ModalWindowService => this;

    public bool Initialize(Action<UIContext> uiDrawCallback, Color4 backgroundColor, float initialScale = 1.0f)
    {
        Console.WriteLine("Win32WindowHost initializing...");

        // Initialize shared resources required by the Win32 backend.
        SharedGraphicsResources.Initialize();

        // Create the window before initializing services, as the window handle is required.
        if (!base.Create())
        {
            Console.WriteLine("Win32WindowHost failed to create its window handle.");
            return false;
        }

        try
        {
            appServices = Win32AppServicesInitializer.Initialize(Handle, GetClientRectSize(), uiDrawCallback, backgroundColor);
            appServices.AppEngine.UIScale = initialScale;
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to initialize Win32WindowHost services: {ex.Message}");
            return false;
        }
    }

    void IWindowHost.Cleanup()
    {
        Cleanup();
    }

    protected override void Cleanup()
    {
        if (_isModalClosing)
        {
            return;
        }

        Console.WriteLine("Win32WindowHost cleaning up its resources...");
        appServices?.AppEngine.Cleanup();
        appServices?.TextService.Cleanup();
        (appServices?.Renderer as Backends.Direct2DRenderer)?.Cleanup();
        appServices?.GraphicsDevice.Cleanup();

        // Cleanup shared resources when the host is disposed.
        SharedGraphicsResources.Cleanup();

        appServices = null;
        activeModalWindow = null;

        base.Cleanup();
    }

    public void RunLoop()
    {
        if (s_windows.Count == 0)
        {
            Console.WriteLine("Win32WindowHost.RunLoop() called with no windows registered.");
            return;
        }

        s_isRunning = true;

        while (s_isRunning)
        {
            ProcessMessages();

            if (!s_isRunning)
            {
                continue;
            }

            foreach (Win32Window? window in s_windows.ToList())
            {
                window.FrameUpdate();
            }
        }
    }

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

    internal static void RegisterWindow(Win32Window window)
    {
        if (!s_windows.Contains(window))
        {
            s_windows.Add(window);
        }
    }

    internal static void UnregisterWindow(Win32Window window)
    {
        s_windows.Remove(window);

        if (s_windows.Count == 0)
        {
            Exit();
        }
    }

    internal static void Exit()
    {
        if (!s_isRunning)
        {
            return;
        }
        s_isRunning = false;
        NativeMethods.PostQuitMessage(0);
    }
    // --- End of logic moved from Application.cs ---

    protected override void OnPaint()
    {
        if (appServices is null || !appServices.GraphicsDevice.IsInitialized)
        {
            Console.WriteLine("Render services not initialized. Skipping paint.");
            return;
        }

        appServices.GraphicsDevice.BeginDraw();
        try
        {
            if (appServices.AppEngine is not null && appServices.GraphicsDevice.RenderTarget is not null)
            {
                appServices.GraphicsDevice.RenderTarget.Clear(appServices.AppEngine.BackgroundColor);
            }

            // If a modal is open, this host window is disabled and should not process UI logic.
            // Just clearing the background is enough to keep it from looking "wiped" by the OS.
            if (!IsModalWindowOpen)
            {
                appServices?.AppEngine?.UpdateAndRender(appServices.Renderer, appServices.TextService);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred during drawing: {ex}");
            appServices.GraphicsDevice.Cleanup();
        }
        finally
        {
            appServices.GraphicsDevice.EndDraw();
        }
    }

    public override void FrameUpdate()
    {
        if (IsModalWindowOpen)
        {
            long currentTicks = _throttleTimer.ElapsedTicks;
            if (currentTicks - _lastModalRepaintTicks > _modalRepaintIntervalTicks)
            {
                OnPaint();
                _lastModalRepaintTicks = currentTicks;
            }
        }
        else
        {
            Invalidate();
        }
        HandleModalLifecycle();
    }

    protected override void OnSize(int width, int height)
    {
        appServices?.GraphicsDevice.Resize(new SizeI(width, height));
    }

    protected override void OnMouseMove(int x, int y)
    {
        appServices?.AppEngine.Input.SetMousePosition(x, y);
        Invalidate();
    }

    protected override void OnMouseDown(MouseButton button, int x, int y)
    {
        appServices?.AppEngine.Input.SetMousePosition(x, y);
        appServices?.AppEngine.Input.SetMouseDown(button);
        Invalidate();
    }

    protected override void OnMouseUp(MouseButton button, int x, int y)
    {
        appServices?.AppEngine.Input.SetMousePosition(x, y);
        appServices?.AppEngine.Input.SetMouseUp(button);
        Invalidate();
    }

    protected override void OnMouseWheel(float delta)
    {
        if (appServices is null) return;

        if (_isCtrlDown)
        {
            float scaleDelta = delta * 0.1f;
            appServices.AppEngine.UIScale = Math.Clamp(appServices.AppEngine.UIScale + scaleDelta, 0.5f, 3.0f);
        }
        else
        {
            appServices.AppEngine.Input.AddMouseWheelDelta(delta);
        }
        Invalidate();
    }

    protected override void OnKeyDown(Keys key)
    {
        if (key == Keys.Control) _isCtrlDown = true;

        appServices?.AppEngine.Input.AddKeyPressed(key);

        if (key == Keys.Escape)
        {
            Close();
        }

        if (key == Keys.F3 && appServices?.AppEngine is not null)
        {
            appServices.AppEngine.ShowFpsCounter = !appServices.AppEngine.ShowFpsCounter;
        }

        Invalidate();
    }

    protected override void OnKeyUp(Keys key)
    {
        if (key == Keys.Control) _isCtrlDown = false;
        appServices?.AppEngine.Input.AddKeyReleased(key);
        Invalidate();
    }

    protected override void OnChar(char c)
    {
        appServices?.AppEngine.Input.AddCharacterInput(c);
        Invalidate();
    }

    protected override bool OnClose()
    {
        return true;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        if (activeModalWindow is null || activeModalWindow.Handle == IntPtr.Zero)
        {
            return;
        }

        activeModalWindow.Close();
    }

    private SizeI GetClientRectSize()
    {
        if (Handle == nint.Zero || !NativeMethods.GetClientRect(Handle, out NativeMethods.RECT r))
        {
            return new(Width, Height);
        }

        int width = int.Max(1, r.right - r.left);
        int height = int.Max(1, r.bottom - r.top);

        return new(width, height);
    }

    public void OpenModalWindow(string title, int width, int height, Action<UIContext> drawCallback, Action<int>? onClosedCallback = null)
    {
        if (activeModalWindow is not null && activeModalWindow.Handle != IntPtr.Zero)
        {
            Console.WriteLine("Warning: Cannot open a new modal window while another is already active.");
            return;
        }

        activeModalWindow = new(this, title, width, height, drawCallback);

        if (activeModalWindow.CreateAsModal())
        {
            OnPaint();
            _lastModalRepaintTicks = _throttleTimer.ElapsedTicks;
            this.onModalClosedCallback = onClosedCallback;
            modalResultCode = -1;
            Console.WriteLine("Modal window opened successfully.");
        }
        else
        {
            Console.WriteLine("Failed to create modal window.");
            activeModalWindow.Dispose();
            activeModalWindow = null;
            onClosedCallback?.Invoke(-1);
        }
    }

    public void CloseModalWindow(int resultCode = 0)
    {
        if (activeModalWindow is null || activeModalWindow.Handle == IntPtr.Zero)
        {
            return;
        }

        modalResultCode = resultCode;
        activeModalWindow.Close();
    }

    /// <summary>
    /// This method is now a fallback for cleanup, but the primary notification
    /// comes from NotifyModalHasClosed.
    /// </summary>
    private void HandleModalLifecycle()
    {
        if (activeModalWindow is null)
        {
            return;
        }

        // If the handle is zero, it means the window was destroyed, but our new
        // notification mechanism might have already handled it. This is a safety net.
        if (activeModalWindow.Handle == IntPtr.Zero && !_isModalClosing)
        {
            Console.WriteLine("[LIFECYCLE-FALLBACK] Cleaning up orphaned modal window.");
            NotifyModalHasClosed();
        }
    }

    /// <summary>
    /// Called directly by the ModalWindow from its OnDestroy method.
    /// This is the new, reliable way to trigger the close callback.
    /// </summary>
    public void NotifyModalHasClosed()
    {
        if (_isModalClosing) return; // Re-entrancy guard
        _isModalClosing = true;

        Console.WriteLine($"Modal window closed. Result: {modalResultCode}");
        onModalClosedCallback?.Invoke(modalResultCode);

        // The modal window is already disposing, so we just clear our references to it.
        activeModalWindow = null;
        onModalClosedCallback = null;
        modalResultCode = 0;
        _isModalClosing = false;
    }

    public bool Initialize(Action<UIContext> uiDrawCallback, Color4 backgroundColor)
    {
        throw new NotImplementedException();
    }
}