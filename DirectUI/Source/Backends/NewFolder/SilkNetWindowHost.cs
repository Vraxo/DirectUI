using System;
using System.Numerics;
using System.Runtime.InteropServices;
using DirectUI.Core;
using DirectUI.Input;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using SkiaSharp;
using Vortice.Mathematics;
using SizeI = Vortice.Mathematics.SizeI;
using Key = Silk.NET.Input.Key;

namespace DirectUI.Backends.SkiaSharp;

public enum WindowBackdropType
{
    Default,
    Mica,
    Acrylic,
    Tabbed,
}

public enum WindowTitleBarTheme
{
    Default,
    Dark,
    Light
}

public class SilkNetWindowHost : Core.IWindowHost, IModalWindowService
{
    private readonly string _title;
    private readonly int _width;
    private readonly int _height;
    private readonly Color4 _backgroundColor;

    // Main window resources
    private IWindow? _window;
    private AppEngine? _appEngine;
    private SilkNetRenderer? _renderer;
    private SilkNetTextService? _textService;
    private GL? _gl;
    private GRContext? _grContext;
    private SKSurface? _skSurface;
    private GRBackendRenderTarget? _renderTarget;
    private IInputContext? _inputContext;
    private bool _isDisposed;

    // Modal window resources and state
    private IWindow? _activeModalIWindow;
    private AppEngine? _modalAppEngine;
    private SilkNetRenderer? _modalRenderer;
    private SilkNetTextService? _modalTextService;
    private IInputContext? _modalInputContext;
    private GL? _modalGl;
    private GRContext? _modalGrContext;
    private SKSurface? _modalSkSurface;
    private GRBackendRenderTarget? _modalRenderTarget;
    private Action<int>? _onModalClosedCallback;
    private int _modalResultCode;

    // P/Invoke for Win32 to enable true modal behavior
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnableWindow(IntPtr hWnd, bool bEnable);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    public IntPtr Handle => _window?.Native.Win32?.Hwnd ?? IntPtr.Zero;
    public InputManager Input => _appEngine?.Input ?? new InputManager();
    public SizeI ClientSize => new(_window?.Size.X ?? 0, _window?.Size.Y ?? 0);
    public bool ShowFpsCounter { get => _appEngine?.ShowFpsCounter ?? false; set { if (_appEngine != null) _appEngine.ShowFpsCounter = value; } }
    public IModalWindowService ModalWindowService => this;
    public bool IsModalWindowOpen => _activeModalIWindow != null;

    public WindowBackdropType BackdropType { get; set; } = WindowBackdropType.Default;
    public WindowTitleBarTheme TitleBarTheme { get; set; } = WindowTitleBarTheme.Dark;

    public SilkNetWindowHost(string title, int width, int height, Color4 backgroundColor)
    {
        _title = title;
        _width = width;
        _height = height;
        _backgroundColor = backgroundColor;
    }

    public bool Initialize(Action<UIContext> uiDrawCallback, Color4 backgroundColor)
    {
        var options = WindowOptions.Default;
        options.Size = new Vector2D<int>(_width, _height);
        options.Title = _title;
        options.API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.Default, new APIVersion(3, 3));
        options.ShouldSwapAutomatically = false; // Required for our manual render loop

        // On modern Windows, always request a transparent framebuffer to allow for effects like Mica/Acrylic.
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            options.TransparentFramebuffer = true;
        }

        _window = Window.Create(options);
        if (_window == null) return false;

        _window.Load += () => OnLoad(uiDrawCallback, backgroundColor);
        _window.Closing += OnClose;
        _window.Resize += OnResize;

        return true;
    }

    public void RunLoop()
    {
        if (_window == null) return;

        _window.Initialize(); // Fires the Load event
        _window.IsVisible = true;

        while (!_window.IsClosing)
        {
            _window.DoEvents(); // Process events for ALL windows on the thread.

            if (IsModalWindowOpen && _activeModalIWindow != null && !_activeModalIWindow.IsClosing)
            {
                // Modal is active. Make its context current before rendering.
                _activeModalIWindow.GLContext?.MakeCurrent();

                _modalGl?.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);
                if (_modalSkSurface != null && _modalRenderer != null && _modalTextService != null && _modalAppEngine != null)
                {
                    _modalRenderer.SetCanvas(_modalSkSurface.Canvas, new Vector2(_activeModalIWindow.Size.X, _activeModalIWindow.Size.Y));
                    _modalAppEngine.UpdateAndRender(_modalRenderer, _modalTextService);
                    _modalSkSurface.Canvas.Flush();
                }
                _activeModalIWindow.SwapBuffers();
            }
            else if (!IsModalWindowOpen)
            {
                // Main window is active. Make its context current before rendering.
                _window.GLContext?.MakeCurrent();
                OnRender(0); // Pass a dummy delta.
                _window.SwapBuffers();
            }

            // Check if a modal that was open is now closing.
            if (IsModalWindowOpen && (_activeModalIWindow == null || _activeModalIWindow.IsClosing))
            {
                HandleModalClose();
            }
        }
    }

    private void HandleModalClose()
    {
        var parentHwnd = Handle;

        // Cleanup modal resources
        _modalAppEngine?.Cleanup();
        _modalRenderer?.Cleanup();
        _modalTextService?.Cleanup();
        _modalSkSurface?.Dispose();
        _modalRenderTarget?.Dispose();
        _modalGrContext?.Dispose();
        _modalGl?.Dispose();
        _modalInputContext?.Dispose();
        _activeModalIWindow?.Dispose();

        // Null out all modal-related fields
        _activeModalIWindow = null;
        _modalAppEngine = null;
        _modalRenderer = null;
        _modalTextService = null;
        _modalInputContext = null;
        _modalGl = null;
        _modalGrContext = null;
        _modalSkSurface = null;
        _modalRenderTarget = null;

        if (parentHwnd != IntPtr.Zero)
        {
            EnableWindow(parentHwnd, true);
            SetForegroundWindow(parentHwnd);
            _window?.Focus();
        }

        Input.HardReset(); // Reset main window input state to prevent stuck keys/buttons

        _onModalClosedCallback?.Invoke(_modalResultCode);
        _onModalClosedCallback = null;
    }

    private void OnLoad(Action<UIContext> uiDrawCallback, Color4 backgroundColor)
    {
        _gl = _window!.CreateOpenGL();

        bool useTransparentBg = BackdropType != WindowBackdropType.Default && OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621);
        Color4 finalBackgroundColor = useTransparentBg ? new Color4(0, 0, 0, 0) : _backgroundColor;

        _gl.ClearColor(finalBackgroundColor.R, finalBackgroundColor.G, finalBackgroundColor.B, finalBackgroundColor.A);

        var glInterface = GRGlInterface.Create();
        _grContext = GRContext.CreateGl(glInterface);

        _renderTarget = new GRBackendRenderTarget(_width, _height, 0, 8, new GRGlFramebufferInfo(0, (uint)GLEnum.Rgba8));
        _skSurface = SKSurface.Create(_grContext, _renderTarget, GRSurfaceOrigin.BottomLeft, SKColorType.Rgba8888);

        _appEngine = new AppEngine(uiDrawCallback, finalBackgroundColor);
        _textService = new SilkNetTextService();
        _renderer = new SilkNetRenderer(_textService);
        _appEngine.Initialize(_textService, _renderer);

        ApplyWindowStyles(); // Apply styles after window handle is available

        _inputContext = _window.CreateInput();
        foreach (var keyboard in _inputContext.Keyboards)
        {
            keyboard.KeyDown += OnKeyDown;
            keyboard.KeyUp += OnKeyUp;
            keyboard.KeyChar += OnKeyChar;
        }
        foreach (var mouse in _inputContext.Mice)
        {
            mouse.MouseDown += OnMouseDown;
            mouse.MouseUp += OnMouseUp;
            mouse.MouseMove += OnMouseMove;
            mouse.Scroll += OnMouseWheel;
        }
    }

    private void OnRender(double delta)
    {
        _gl?.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);

        if (_skSurface is null || _renderer is null || _textService is null || _appEngine is null) return;

        _renderer.SetCanvas(_skSurface.Canvas, new Vector2(_window!.Size.X, _window.Size.Y));
        _appEngine.UpdateAndRender(_renderer, _textService);
        _skSurface.Canvas.Flush();
    }

    private void OnResize(Vector2D<int> size)
    {
        _gl?.Viewport(size);

        _renderTarget?.Dispose();
        _skSurface?.Dispose();

        _renderTarget = new GRBackendRenderTarget(size.X, size.Y, 0, 8, new GRGlFramebufferInfo(0, (uint)GLEnum.Rgba8));
        _skSurface = SKSurface.Create(_grContext, _renderTarget, GRSurfaceOrigin.BottomLeft, SKColorType.Rgba8888);
    }

    private void OnClose()
    {
        // The RunLoop will exit when this handler returns.
        // Cleanup is handled by the finally block in ApplicationRunner.
    }

    private void ApplyWindowStyles()
    {
        if (!OperatingSystem.IsWindows()) return;

        var hwnd = _window?.Native.Win32?.Hwnd ?? IntPtr.Zero;
        if (hwnd == IntPtr.Zero) return;

        // Set dark mode before backdrop type for better transition
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763))
        {
            if (TitleBarTheme != WindowTitleBarTheme.Default)
            {
                int useDarkMode = (TitleBarTheme == WindowTitleBarTheme.Dark) ? 1 : 0;
                DwmApi.DwmSetWindowAttribute(hwnd, DwmApi.DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDarkMode, sizeof(int));
            }
        }

        // Set backdrop type (Mica/Acrylic) - requires Windows 11 Build 22621+
        if (BackdropType != WindowBackdropType.Default && OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621))
        {
            int backdropValue = BackdropType switch
            {
                WindowBackdropType.Mica => (int)DwmApi.DWMSYSTEMBACKDROP_TYPE.DWMSBT_MAINWINDOW,
                WindowBackdropType.Acrylic => (int)DwmApi.DWMSYSTEMBACKDROP_TYPE.DWMSBT_TRANSIENTWINDOW,
                WindowBackdropType.Tabbed => (int)DwmApi.DWMSYSTEMBACKDROP_TYPE.DWMSBT_TABBEDWINDOW,
                _ => (int)DwmApi.DWMSYSTEMBACKDROP_TYPE.DWMSBT_AUTO
            };
            DwmApi.DwmSetWindowAttribute(hwnd, DwmApi.DWMWINDOWATTRIBUTE.DWMWA_SYSTEMBACKDROP_TYPE, ref backdropValue, sizeof(int));
        }
    }

    private void OnKeyDown(IKeyboard keyboard, Key key, int scancode) { if (!IsModalWindowOpen) Input.AddKeyPressed(MapKey(key)); }
    private void OnKeyUp(IKeyboard keyboard, Key key, int scancode) { if (!IsModalWindowOpen) Input.AddKeyReleased(MapKey(key)); }
    private void OnKeyChar(IKeyboard keyboard, char c) { if (!IsModalWindowOpen) Input.AddCharacterInput(c); }
    private void OnMouseDown(IMouse mouse, Silk.NET.Input.MouseButton button) { if (!IsModalWindowOpen) Input.SetMouseDown(MapMouseButton(button)); }
    private void OnMouseUp(IMouse mouse, Silk.NET.Input.MouseButton button) { if (!IsModalWindowOpen) Input.SetMouseUp(MapMouseButton(button)); }
    private void OnMouseMove(IMouse mouse, Vector2 position) { if (!IsModalWindowOpen) Input.SetMousePosition((int)position.X, (int)position.Y); }
    private void OnMouseWheel(IMouse mouse, ScrollWheel scroll) { if (!IsModalWindowOpen) Input.AddMouseWheelDelta(scroll.Y); }

    public void OpenModalWindow(string title, int width, int height, Action<UIContext> drawCallback, Action<int>? onClosedCallback = null)
    {
        if (IsModalWindowOpen || _window is null) return;

        _onModalClosedCallback = onClosedCallback;
        _modalResultCode = -1; // Default to canceled/closed

        var parentHwnd = Handle;
        if (parentHwnd != IntPtr.Zero)
        {
            EnableWindow(parentHwnd, false);
        }

        var options = WindowOptions.Default;
        options.Size = new Vector2D<int>(width, height);
        options.Title = title;
        options.API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.Default, new APIVersion(3, 3));
        options.WindowBorder = WindowBorder.Fixed;
        options.ShouldSwapAutomatically = false;

        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            options.TransparentFramebuffer = true;
        }

        _activeModalIWindow = Window.Create(options);

        _activeModalIWindow.Load += () =>
        {
            _modalGl = _activeModalIWindow.CreateOpenGL();
            var modalBgColor = new Color4(60 / 255f, 60 / 255f, 60 / 255f, 1.0f);
            bool useTransparentModalBg = OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621);

            if (useTransparentModalBg)
            {
                _modalGl.ClearColor(0, 0, 0, 0);
                modalBgColor = new Color4(0, 0, 0, 0);

                var modalHwnd = _activeModalIWindow.Native.Win32?.Hwnd ?? IntPtr.Zero;
                if (modalHwnd != IntPtr.Zero)
                {
                    if (TitleBarTheme != WindowTitleBarTheme.Default)
                    {
                        int useDarkMode = (TitleBarTheme == WindowTitleBarTheme.Dark) ? 1 : 0;
                        DwmApi.DwmSetWindowAttribute(modalHwnd, DwmApi.DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDarkMode, sizeof(int));
                    }

                    int backdropValue = (int)DwmApi.DWMSYSTEMBACKDROP_TYPE.DWMSBT_TRANSIENTWINDOW; // Acrylic for modals
                    DwmApi.DwmSetWindowAttribute(modalHwnd, DwmApi.DWMWINDOWATTRIBUTE.DWMWA_SYSTEMBACKDROP_TYPE, ref backdropValue, sizeof(int));
                }
            }
            else
            {
                _modalGl.ClearColor(modalBgColor.R, modalBgColor.G, modalBgColor.B, modalBgColor.A);
            }

            var glInterface = GRGlInterface.Create();
            _modalGrContext = GRContext.CreateGl(glInterface);
            _modalRenderTarget = new GRBackendRenderTarget(width, height, 0, 8, new GRGlFramebufferInfo(0, (uint)GLEnum.Rgba8));
            _modalSkSurface = SKSurface.Create(_modalGrContext, _modalRenderTarget, GRSurfaceOrigin.BottomLeft, SKColorType.Rgba8888);

            _modalAppEngine = new AppEngine(drawCallback, modalBgColor);
            _modalTextService = new SilkNetTextService();
            _modalRenderer = new SilkNetRenderer(_modalTextService);
            _modalAppEngine.Initialize(_modalTextService, _modalRenderer);

            _modalInputContext = _activeModalIWindow.CreateInput();
            foreach (var kbd in _modalInputContext.Keyboards)
            {
                kbd.KeyDown += (_, key, _) => _modalAppEngine.Input.AddKeyPressed(MapKey(key));
                kbd.KeyUp += (_, key, _) => _modalAppEngine.Input.AddKeyReleased(MapKey(key));
                kbd.KeyChar += (_, c) => _modalAppEngine.Input.AddCharacterInput(c);
            }
            foreach (var m in _modalInputContext.Mice)
            {
                m.MouseDown += (_, btn) => _modalAppEngine.Input.SetMouseDown(MapMouseButton(btn));
                m.MouseUp += (_, btn) => _modalAppEngine.Input.SetMouseUp(MapMouseButton(btn));
                m.MouseMove += (_, pos) => _modalAppEngine.Input.SetMousePosition((int)pos.X, (int)pos.Y);
                m.Scroll += (_, scroll) => _modalAppEngine.Input.AddMouseWheelDelta(scroll.Y);
            }
        };

        _activeModalIWindow.Initialize();
        _activeModalIWindow.IsVisible = true;
    }

    public void CloseModalWindow(int resultCode = 0)
    {
        if (!IsModalWindowOpen) return;
        _modalResultCode = resultCode;
        _activeModalIWindow?.Close();
    }

    private static Keys MapKey(Key key) => key switch
    {
        Key.Space => Keys.Space,
        Key.Apostrophe => Keys.Unknown,
        Key.Comma => Keys.Unknown,
        Key.Minus => Keys.Unknown,
        Key.Period => Keys.Unknown,
        Key.Slash => Keys.Unknown,
        Key.Number0 => Keys.D0,
        Key.Number1 => Keys.D1,
        Key.Number2 => Keys.D2,
        Key.Number3 => Keys.D3,
        Key.Number4 => Keys.D4,
        Key.Number5 => Keys.D5,
        Key.Number6 => Keys.D6,
        Key.Number7 => Keys.D7,
        Key.Number8 => Keys.D8,
        Key.Number9 => Keys.D9,
        Key.Semicolon => Keys.Unknown,
        Key.Equal => Keys.Unknown,
        Key.A => Keys.A,
        Key.B => Keys.B,
        Key.C => Keys.C,
        Key.D => Keys.D,
        Key.E => Keys.E,
        Key.F => Keys.F,
        Key.G => Keys.G,
        Key.H => Keys.H,
        Key.I => Keys.I,
        Key.J => Keys.J,
        Key.K => Keys.K,
        Key.L => Keys.L,
        Key.M => Keys.M,
        Key.N => Keys.N,
        Key.O => Keys.O,
        Key.P => Keys.P,
        Key.Q => Keys.Q,
        Key.R => Keys.R,
        Key.S => Keys.S,
        Key.T => Keys.T,
        Key.U => Keys.U,
        Key.V => Keys.V,
        Key.W => Keys.W,
        Key.X => Keys.X,
        Key.Y => Keys.Y,
        Key.Z => Keys.Z,
        Key.LeftBracket => Keys.Unknown,
        Key.BackSlash => Keys.Unknown,
        Key.RightBracket => Keys.Unknown,
        Key.GraveAccent => Keys.Unknown,
        Key.World1 => Keys.Unknown,
        Key.World2 => Keys.Unknown,
        Key.Escape => Keys.Escape,
        Key.Enter => Keys.Enter,
        Key.Tab => Keys.Tab,
        Key.Backspace => Keys.Backspace,
        Key.Insert => Keys.Insert,
        Key.Delete => Keys.Delete,
        Key.Right => Keys.RightArrow,
        Key.Left => Keys.LeftArrow,
        Key.Down => Keys.DownArrow,
        Key.Up => Keys.UpArrow,
        Key.PageUp => Keys.PageUp,
        Key.PageDown => Keys.PageDown,
        Key.Home => Keys.Home,
        Key.End => Keys.End,
        Key.CapsLock => Keys.CapsLock,
        Key.ScrollLock => Keys.Unknown,
        Key.NumLock => Keys.Unknown,
        Key.PrintScreen => Keys.Unknown,
        Key.Pause => Keys.Pause,
        Key.F1 => Keys.F1,
        Key.F2 => Keys.F2,
        Key.F3 => Keys.F3,
        Key.F4 => Keys.F4,
        Key.F5 => Keys.F5,
        Key.F6 => Keys.F6,
        Key.F7 => Keys.F7,
        Key.F8 => Keys.F8,
        Key.F9 => Keys.F9,
        Key.F10 => Keys.F10,
        Key.F11 => Keys.F11,
        Key.F12 => Keys.F12,
        Key.Keypad0 => Keys.D0,
        Key.Keypad1 => Keys.D1,
        Key.Keypad2 => Keys.D2,
        Key.Keypad3 => Keys.D3,
        Key.Keypad4 => Keys.D4,
        Key.Keypad5 => Keys.D5,
        Key.Keypad6 => Keys.D6,
        Key.Keypad7 => Keys.D7,
        Key.Keypad8 => Keys.D8,
        Key.Keypad9 => Keys.D9,
        Key.ShiftLeft => Keys.Shift,
        Key.ShiftRight => Keys.Shift,
        Key.ControlLeft => Keys.Control,
        Key.ControlRight => Keys.Control,
        Key.AltLeft => Keys.Alt,
        Key.AltRight => Keys.Alt,
        Key.SuperLeft => Keys.LeftWindows,
        Key.SuperRight => Keys.RightWindows,
        Key.Menu => Keys.Menu,
        _ => Keys.Unknown,
    };

    private static DirectUI.MouseButton MapMouseButton(Silk.NET.Input.MouseButton button) => button switch
    {
        Silk.NET.Input.MouseButton.Left => DirectUI.MouseButton.Left,
        Silk.NET.Input.MouseButton.Right => DirectUI.MouseButton.Right,
        Silk.NET.Input.MouseButton.Middle => DirectUI.MouseButton.Middle,
        Silk.NET.Input.MouseButton.Button4 => DirectUI.MouseButton.XButton1,
        Silk.NET.Input.MouseButton.Button5 => DirectUI.MouseButton.XButton2,
        _ => DirectUI.MouseButton.Left,
    };

    public void Cleanup()
    {
        if (_isDisposed) return;
        HandleModalClose(); // Ensure modal resources are cleaned up if it was open
        _appEngine?.Cleanup();
        _renderer?.Cleanup();
        _textService?.Cleanup();
        _skSurface?.Dispose();
        _renderTarget?.Dispose();
        _grContext?.Dispose();
        _gl?.Dispose();
        _inputContext?.Dispose();
        _window?.Dispose();
        _isDisposed = true;
    }

    public void Dispose()
    {
        Cleanup();
        GC.SuppressFinalize(this);
    }
}