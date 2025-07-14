using DirectUI.Core;
using DirectUI.Input;
using Vortice.Mathematics;
using SizeI = Vortice.Mathematics.SizeI;

namespace DirectUI;

public class Win32WindowHost : Win32Window, IWindowHost, IModalWindowService
{
    private AppServices? _appServices; // Changed to the new bundle

    private ModalWindow? _activeModalWindow;
    private int _modalResultCode;
    private Action<int>? _onModalClosedCallback;
    private bool _isModalClosing;

    public bool IsModalWindowOpen
    {
        get
        {
            return _activeModalWindow is not null
                && _activeModalWindow.Handle != IntPtr.Zero;
        }
    }

    public Win32WindowHost(string title = "DirectUI Win32 Host", int width = 800, int height = 600)
        : base(title, width, height)
    {
    }

    public InputManager Input => _appServices?.AppEngine.Input ?? new();
    public SizeI ClientSize => GetClientRectSize();

    public bool ShowFpsCounter
    {
        get
        {
            return _appServices?.AppEngine.ShowFpsCounter ?? false;
        }

        set
        {
            if (_appServices is null)
            {
                return;
            }

            _appServices.AppEngine.ShowFpsCounter = value;
        }
    }

    public IModalWindowService ModalWindowService => this;

    public bool Initialize(Action<UIContext> uiDrawCallback, Color4 backgroundColor)
    {
        Console.WriteLine("Win32WindowHost initializing...");

        // Create the window before initializing services, as the window handle is required.
        if (!base.Create())
        {
            Console.WriteLine("Win32WindowHost failed to create its window handle.");
            return false;
        }

        try
        {
            _appServices = Win32AppServicesInitializer.Initialize(Handle, GetClientRectSize(), uiDrawCallback, backgroundColor);
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
        _appServices?.AppEngine.Cleanup();
        _appServices?.TextService.Cleanup();
        (_appServices?.Renderer as DirectUI.Backends.Direct2DRenderer)?.Cleanup();
        _appServices?.GraphicsDevice.Cleanup();

        _appServices = null; // Clear the bundle
        _activeModalWindow = null;

        base.Cleanup();
    }

    public void RunLoop()
    {
        Application.RunMessageLoop();
    }

    protected override void OnPaint()
    {
        if (_appServices is null || !_appServices.GraphicsDevice.IsInitialized)
        {
            Console.WriteLine("Render services not initialized. Skipping paint.");
            return;
        }

        _appServices.GraphicsDevice.BeginDraw();

        try
        {
            // Clear the background before drawing anything else. This fixes smearing artifacts.
            if (_appServices.AppEngine is not null && _appServices.GraphicsDevice.RenderTarget is not null)
            {
                _appServices.GraphicsDevice.RenderTarget.Clear(_appServices.AppEngine.BackgroundColor);
            }

            _appServices.AppEngine.UpdateAndRender(_appServices.Renderer, _appServices.TextService);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred during drawing: {ex}");
            _appServices.GraphicsDevice.Cleanup();
        }
        finally
        {
            _appServices.GraphicsDevice.EndDraw();
        }
    }

    public override void FrameUpdate()
    {
        Invalidate();
        HandleModalLifecycle();
    }

    protected override void OnSize(int width, int height)
    {
        _appServices?.GraphicsDevice.Resize(new SizeI(width, height));
    }

    protected override void OnMouseMove(int x, int y)
    {
        _appServices?.AppEngine.Input.SetMousePosition(x, y);
        Invalidate();
    }

    protected override void OnMouseDown(MouseButton button, int x, int y)
    {
        _appServices?.AppEngine.Input.SetMousePosition(x, y);
        _appServices?.AppEngine.Input.SetMouseDown(button);
        Invalidate();
    }

    protected override void OnMouseUp(MouseButton button, int x, int y)
    {
        _appServices?.AppEngine.Input.SetMousePosition(x, y);
        _appServices?.AppEngine.Input.SetMouseUp(button);
        Invalidate();
    }

    protected override void OnMouseWheel(float delta)
    {
        _appServices?.AppEngine.Input.AddMouseWheelDelta(delta);
        Invalidate();
    }

    protected override void OnKeyDown(Keys key)
    {
        _appServices?.AppEngine.Input.AddKeyPressed(key);

        if (key == Keys.Escape)
        {
            Close();
        }

        if (key == Keys.F3 && _appServices?.AppEngine is not null)
        {
            _appServices.AppEngine.ShowFpsCounter = !_appServices.AppEngine.ShowFpsCounter;
        }

        Invalidate();
    }

    protected override void OnKeyUp(Keys key)
    {
        _appServices?.AppEngine.Input.AddKeyReleased(key);
        Invalidate();
    }

    protected override void OnChar(char c)
    {
        _appServices?.AppEngine.Input.AddCharacterInput(c);
        Invalidate();
    }

    protected override bool OnClose()
    {
        return true;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        if (_activeModalWindow is null || _activeModalWindow.Handle == IntPtr.Zero)
        {
            return;
        }

        _activeModalWindow.Close();
    }

    private SizeI GetClientRectSize()
    {
        if (Handle == nint.Zero || !NativeMethods.GetClientRect(Handle, out NativeMethods.RECT r))
        {
            return new(Width, Height);
        }

        int width = Math.Max(1, r.right - r.left);
        int height = Math.Max(1, r.bottom - r.top);

        return new(width, height);
    }

    public void OpenModalWindow(string title, int width, int height, Action<UIContext> drawCallback, Action<int>? onClosedCallback = null)
    {
        if (_activeModalWindow is not null && _activeModalWindow.Handle != IntPtr.Zero)
        {
            Console.WriteLine("Warning: Cannot open a new modal window while another is already active.");
            return;
        }

        _activeModalWindow = new(this, title, width, height, drawCallback);

        if (_activeModalWindow.CreateAsModal())
        {
            _onModalClosedCallback = onClosedCallback;
            _modalResultCode = -1;
            Console.WriteLine("Modal window opened successfully.");
        }
        else
        {
            Console.WriteLine("Failed to create modal window.");
            _activeModalWindow.Dispose();
            _activeModalWindow = null;
            onClosedCallback?.Invoke(-1);
        }
    }

    public void CloseModalWindow(int resultCode = 0)
    {
        if (_activeModalWindow is null || _activeModalWindow.Handle == IntPtr.Zero)
        {
            return;
        }

        _modalResultCode = resultCode;
        _activeModalWindow.Close();
    }

    private void HandleModalLifecycle()
    {
        if (_activeModalWindow is null)
        {
            return;
        }

        if (_activeModalWindow.Handle != IntPtr.Zero || _isModalClosing)
        {
            return;
        }

        _isModalClosing = true;

        Console.WriteLine($"Modal window closed. Result: {_modalResultCode}");
        _onModalClosedCallback?.Invoke(_modalResultCode);

        _activeModalWindow.Dispose();
        _activeModalWindow = null;
        _onModalClosedCallback = null;
        _modalResultCode = 0;
        _isModalClosing = false;
    }
}