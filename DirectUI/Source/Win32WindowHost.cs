using DirectUI.Core;
using DirectUI.Input;
using Vortice.Mathematics;
using SizeI = Vortice.Mathematics.SizeI;

namespace DirectUI;

public class Win32WindowHost : Win32Window, IWindowHost, IModalWindowService
{
    private AppEngine? _appEngine;
    private DuiGraphicsDevice? _graphicsDevice;
    private IRenderer? _renderer;
    private ITextService? _textService;

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

    public InputManager Input => _appEngine?.Input ?? new();
    public SizeI ClientSize => GetClientRectSize();

    public bool ShowFpsCounter
    {
        get
        {
            return _appEngine?.ShowFpsCounter ?? false;
        }

        set
        {
            if (_appEngine is null)
            {
                return;
            }

            _appEngine.ShowFpsCounter = value;
        }
    }

    public IModalWindowService ModalWindowService => this;

    public bool Initialize(Action<UIContext> uiDrawCallback, Color4 backgroundColor)
    {
        Console.WriteLine("Win32WindowHost initializing...");
        
        if (!base.Initialize())
        {
            return false;
        }

        _appEngine = new(uiDrawCallback, backgroundColor);

        _graphicsDevice = new();

        if (!_graphicsDevice.Initialize(Handle, GetClientRectSize()))
        {
            Console.WriteLine("Failed to initialize DuiGraphicsDevice.");
            return false;
        }

        if (_graphicsDevice.RenderTarget is not null && _graphicsDevice.DWriteFactory is not null)
        {
            _renderer = new Backends.Direct2DRenderer(_graphicsDevice.RenderTarget, _graphicsDevice.DWriteFactory);
            _textService = new Backends.DirectWriteTextService(_graphicsDevice.DWriteFactory);
        }
        else
        {
            Console.WriteLine("CRITICAL: GraphicsDevice did not provide valid RenderTarget or DWriteFactory for Direct2D backend initialization.");
            return false;
        }

        _appEngine.Initialize(_textService, _renderer);
        return true;
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
        _appEngine?.Cleanup();
        _textService?.Cleanup();
        (_renderer as DirectUI.Backends.Direct2DRenderer)?.Cleanup();
        _graphicsDevice?.Cleanup();

        _appEngine = null;
        _renderer = null;
        _textService = null;
        _graphicsDevice = null;
        _activeModalWindow = null;

        base.Cleanup();
    }

    public void RunLoop()
    {
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
            _graphicsDevice.Cleanup();
        }
        finally
        {
            _graphicsDevice.EndDraw();
        }
    }

    public override void FrameUpdate()
    {
        Invalidate();
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
        _appEngine?.Input.SetMousePosition(x, y);
        _appEngine?.Input.SetMouseDown(button);
        Invalidate();
    }

    protected override void OnMouseUp(MouseButton button, int x, int y)
    {
        _appEngine?.Input.SetMousePosition(x, y);
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

        if (key == Keys.F3 && _appEngine is not null)
        {
            _appEngine.ShowFpsCounter = !_appEngine.ShowFpsCounter;
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