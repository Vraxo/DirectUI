// Win32WindowHost.cs
using System;
using System.Numerics;
using DirectUI.Core; // Added for IWindowHost, IModalWindowService
using DirectUI.Input;
using Vortice.Direct2D1; // For AntialiasMode
using Vortice.DirectWrite; // For DirectWriteTextService
using Vortice.Mathematics;
using SizeI = Vortice.Mathematics.SizeI;

namespace DirectUI;

/// <summary>
/// A concrete implementation of <see cref="IWindowHost"/> for Win32 and Direct2D.
/// It also implements <see cref="IModalWindowService"/> for Win32 modal dialogues.
/// </summary>
public class Win32WindowHost : Win32Window, IWindowHost, IModalWindowService
{
    private AppEngine? _appEngine;
    private DuiGraphicsDevice? _graphicsDevice;
    private IRenderer? _renderer;
    private ITextService? _textService;

    // Modal Window State
    private ModalWindow? _activeModalWindow;
    private int _modalResultCode;
    private Action<int>? _onModalClosedCallback;
    private bool _isModalClosing; // Flag to prevent re-entry during modal cleanup

    public Win32WindowHost(string title = "DirectUI Win32 Host", int width = 800, int height = 600)
        : base(title, width, height)
    {
    }

    public InputManager Input => _appEngine?.Input ?? new InputManager(); // Provide AppEngine's input manager
    public SizeI ClientSize => GetClientRectSize();

    public bool ShowFpsCounter
    {
        get => _appEngine?.ShowFpsCounter ?? false;
        set { if (_appEngine != null) _appEngine.ShowFpsCounter = value; }
    }

    public IModalWindowService ModalWindowService => this; // This class *is* the modal window service

    public bool Initialize(Action<UIContext> uiDrawCallback, Color4 backgroundColor)
    {
        Console.WriteLine("Win32WindowHost initializing...");
        if (!base.Initialize()) // Call base Win32Window initialization (registers class, creates HWND)
        {
            return false;
        }

        _appEngine = new AppEngine(uiDrawCallback, backgroundColor);

        // Initialize Direct2D specific graphics resources
        _graphicsDevice = new DuiGraphicsDevice();
        if (!_graphicsDevice.Initialize(Handle, GetClientRectSize()))
        {
            Console.WriteLine("Failed to initialize DuiGraphicsDevice.");
            return false;
        }

        // Initialize backend services using the concrete D2D and DWrite factories
        if (_graphicsDevice.RenderTarget != null && _graphicsDevice.DWriteFactory != null)
        {
            _renderer = new DirectUI.Backends.Direct2DRenderer(_graphicsDevice.RenderTarget, _graphicsDevice.DWriteFactory);
            _textService = new DirectUI.Backends.DirectWriteTextService(_graphicsDevice.DWriteFactory);
        }
        else
        {
            Console.WriteLine("CRITICAL: GraphicsDevice did not provide valid RenderTarget or DWriteFactory for Direct2D backend initialization.");
            return false;
        }

        _appEngine.Initialize(_textService, _renderer); // Initialize AppEngine's internal components
        return true;
    }

    void IWindowHost.Cleanup()
    {
        // Call the protected override Cleanup method from this class
        this.Cleanup();
    }

    protected override void Cleanup() // Retain protected override for internal consistency
    {
        if (_isModalClosing) return; // Prevent re-entry if modal is already cleaning up

        Console.WriteLine("Win32WindowHost cleaning up its resources...");
        _appEngine?.Cleanup();
        _textService?.Cleanup();
        (_renderer as DirectUI.Backends.Direct2DRenderer)?.Cleanup();
        _graphicsDevice?.Cleanup(); // Dispose of Direct2D graphics resources

        _appEngine = null;
        _renderer = null;
        _textService = null;
        _graphicsDevice = null;
        _activeModalWindow = null; // Ensure modal reference is cleared

        base.Cleanup(); // Call base class cleanup
    }

    public void RunLoop()
    {
        // The main application loop for Win32.
        // This will block and process messages, calling OnPaint/FrameUpdate.
        Application.RunMessageLoop();
    }

    protected override void OnPaint()
    {
        if (_graphicsDevice is null || _renderer is null || _textService is null || _appEngine is null)
        {
            Console.WriteLine("Render services not initialized. Skipping paint.");
            return;
        }

        _graphicsDevice.BeginDraw();
        try
        {
            _appEngine.UpdateAndRender(_renderer, _textService);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred during drawing: {ex}");
            _graphicsDevice.Cleanup(); // Try to recover by cleaning graphics device
        }
        finally
        {
            _graphicsDevice.EndDraw();
        }
    }

    public override void FrameUpdate()
    {
        // For Win32/Direct2D, FrameUpdate means requesting a paint.
        // The actual drawing happens in OnPaint triggered by WM_PAINT.
        Invalidate();

        // Handle modal window lifecycle outside of WM_PAINT to avoid re-entrancy.
        HandleModalLifecycle();
    }

    protected override void OnSize(int width, int height)
    {
        _graphicsDevice?.Resize(new SizeI(width, height));
    }

    protected override void OnMouseMove(int x, int y)
    {
        _appEngine?.Input.SetMousePosition(x, y);
        Invalidate();
    }

    protected override void OnMouseDown(MouseButton button, int x, int y)
    {
        _appEngine?.Input.SetMousePosition(x, y); // Update position on click
        _appEngine?.Input.SetMouseDown(button);
        Invalidate();
    }

    protected override void OnMouseUp(MouseButton button, int x, int y)
    {
        _appEngine?.Input.SetMousePosition(x, y); // Update position on release
        _appEngine?.Input.SetMouseUp(button);
        Invalidate();
    }

    protected override void OnMouseWheel(float delta)
    {
        _appEngine?.Input.AddMouseWheelDelta(delta);
        Invalidate();
    }

    protected override void OnKeyDown(Keys key)
    {
        _appEngine?.Input.AddKeyPressed(key);

        if (key == Keys.Escape)
        {
            Close();
        }
        if (key == Keys.F3)
        {
            if (_appEngine != null) _appEngine.ShowFpsCounter = !_appEngine.ShowFpsCounter;
        }
        Invalidate();
    }

    protected override void OnKeyUp(Keys key)
    {
        _appEngine?.Input.AddKeyReleased(key);
        Invalidate();
    }

    protected override void OnChar(char c)
    {
        _appEngine?.Input.AddCharacterInput(c);
        Invalidate();
    }

    protected override bool OnClose()
    {
        return true; // Allow window to close
    }

    protected override void OnDestroy()
    {
        // Base OnDestroy unregisters the window and triggers Application.Exit if last window.
        base.OnDestroy();

        // If a modal window is open when the owner closes, ensure it's also closed.
        if (_activeModalWindow != null && _activeModalWindow.Handle != IntPtr.Zero)
        {
            _activeModalWindow.Close(); // This will trigger its own OnDestroy/Cleanup
        }
    }

    private SizeI GetClientRectSize()
    {
        if (Handle != nint.Zero && NativeMethods.GetClientRect(Handle, out NativeMethods.RECT r))
        {
            int width = Math.Max(1, r.right - r.left);
            int height = Math.Max(1, r.bottom - r.top);
            return new SizeI(width, height);
        }
        // Fallback for when the handle is not yet valid during initialization or if GetClientRect fails.
        // This size isn't critical as resize will be called immediately after if the window is created.
        return new SizeI(Width, Height); // Use stored initial window size.
    }

    // --- IModalWindowService Implementation ---
    public void OpenModalWindow(string title, int width, int height, Action<UIContext> drawCallback, Action<int>? onClosedCallback = null)
    {
        if (_activeModalWindow != null && _activeModalWindow.Handle != IntPtr.Zero)
        {
            Console.WriteLine("Warning: Cannot open a new modal window while another is already active.");
            return;
        }

        _activeModalWindow = new ModalWindow(this, title, width, height, drawCallback);
        if (_activeModalWindow.CreateAsModal())
        {
            _onModalClosedCallback = onClosedCallback;
            _modalResultCode = -1; // Default to an "unset" or "error" state
            Console.WriteLine("Modal window opened successfully.");
        }
        else
        {
            Console.WriteLine("Failed to create modal window.");
            _activeModalWindow.Dispose();
            _activeModalWindow = null;
            onClosedCallback?.Invoke(-1); // Indicate failure to the caller
        }
    }

    public void CloseModalWindow(int resultCode = 0)
    {
        if (_activeModalWindow != null && _activeModalWindow.Handle != IntPtr.Zero)
        {
            _modalResultCode = resultCode;
            _activeModalWindow.Close(); // This will trigger WM_DESTROY
        }
    }

    public bool IsModalWindowOpen => _activeModalWindow != null && _activeModalWindow.Handle != IntPtr.Zero;

    /// <summary>
    /// Manages the lifecycle of the modal window, checking its state each frame.
    /// </summary>
    private void HandleModalLifecycle()
    {
        if (_activeModalWindow == null) return;

        if (_activeModalWindow.Handle == IntPtr.Zero && !_isModalClosing)
        {
            // The modal window has been destroyed (e.g., by user clicking X or our CloseModalWindow call).
            _isModalClosing = true; // Set flag to prevent re-entry
            Console.WriteLine($"Modal window closed. Result: {_modalResultCode}");
            _onModalClosedCallback?.Invoke(_modalResultCode); // Notify caller of closure and result

            _activeModalWindow.Dispose(); // Ensure all managed resources are cleaned up
            _activeModalWindow = null;
            _onModalClosedCallback = null;
            _modalResultCode = 0;
            _isModalClosing = false; // Reset flag
        }
    }
}
