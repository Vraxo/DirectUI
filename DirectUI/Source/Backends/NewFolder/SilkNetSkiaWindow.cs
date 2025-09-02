using System;
using System.Numerics;
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

public class SilkNetSkiaWindow : IDisposable
{
    private readonly SilkNetWindowHost _owner;
    private readonly bool _isModal;
    private readonly Vector2D<int>? _initialPosition;

    internal IWindow IWindow { get; }
    private AppEngine? _appEngine;
    private SilkNetRenderer? _renderer;
    private SilkNetTextService? _textService;
    private GL? _gl;
    private GRContext? _grContext;
    private SKSurface? _skSurface;
    private GRBackendRenderTarget? _renderTarget;
    private IInputContext? _inputContext;
    private bool _isDisposed;

    public IntPtr Handle => IWindow.Native.Win32?.Hwnd ?? IntPtr.Zero;
    public InputManager Input => _appEngine?.Input ?? new InputManager();
    public SizeI ClientSize => new(IWindow.Size.X, IWindow.Size.Y);
    public bool ShowFpsCounter
    {
        get => _appEngine?.ShowFpsCounter ?? false;
        set { if (_appEngine != null) _appEngine.ShowFpsCounter = value; }
    }

    public SilkNetSkiaWindow(
        string title,
        int width,
        int height,
        SilkNetWindowHost owner,
        bool isModal,
        Vector2D<int>? initialPosition = null)
    {
        _owner = owner;
        _isModal = isModal;
        _initialPosition = initialPosition;

        var options = WindowOptions.Default;

        // 1) constructor-time hint
        if (_initialPosition.HasValue)
            options.Position = _initialPosition.Value;

        options.Size = new Vector2D<int>(width, height);
        options.Title = title;
        options.API = new GraphicsAPI(
            ContextAPI.OpenGL,
            ContextProfile.Core,
            ContextFlags.Default,
            new APIVersion(3, 3)
        );
        options.ShouldSwapAutomatically = false;
        options.WindowBorder = isModal
            ? WindowBorder.Fixed
            : WindowBorder.Resizable;

        // keep hidden until we’re fully configured
        options.IsVisible = false;

        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
            options.TransparentFramebuffer = true;

        IWindow = Window.Create(options);
    }

    public bool Initialize(Action<UIContext> uiDrawCallback, Color4 backgroundColor)
    {
        if (IWindow == null) return false;

        IWindow.Load += () => OnLoad(uiDrawCallback, backgroundColor);
        IWindow.Closing += OnClose;
        IWindow.Resize += OnResize;

        return true;
    }

    private void OnLoad(Action<UIContext> uiDrawCallback, Color4 backgroundColor)
    {
        // 3) final reposition safe-guard
        if (_initialPosition.HasValue)
            IWindow.Position = _initialPosition.Value;

        _gl = IWindow.CreateOpenGL();
        ApplyWindowStyles();

        bool useTransparentBg = _owner.BackdropType != WindowBackdropType.Default
                                && OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621);

        var finalBg = useTransparentBg
            ? (_owner.TitleBarTheme == WindowTitleBarTheme.Light
                ? new Color4(243 / 255f, 243 / 255f, 243 / 255f, 1 / 255f)
                : new Color4(0, 0, 0, 1 / 255f))
            : backgroundColor;

        _gl.ClearColor(finalBg.R, finalBg.G, finalBg.B, finalBg.A);

        var glInterface = GRGlInterface.Create();
        _grContext = GRContext.CreateGl(glInterface);

        // build the initial Skia surface
        OnResize(IWindow.Size);

        _appEngine = new AppEngine(uiDrawCallback, finalBg);
        _textService = new SilkNetTextService();
        _renderer = new SilkNetRenderer(_textService);
        _appEngine.Initialize(_textService, _renderer);

        _inputContext = IWindow.CreateInput();
        foreach (var kb in _inputContext.Keyboards)
        {
            kb.KeyDown += OnKeyDown;
            kb.KeyUp += OnKeyUp;
            kb.KeyChar += OnKeyChar;
        }
        foreach (var m in _inputContext.Mice)
        {
            m.MouseDown += OnMouseDown;
            m.MouseUp += OnMouseUp;
            m.MouseMove += OnMouseMove;
            m.Scroll += OnMouseWheel;
        }
    }

    public void Render()
    {
        if (IWindow.IsClosing
            || _skSurface is null
            || _renderer is null
            || _textService is null
            || _appEngine is null
            || _gl is null)
            return;

        IWindow.GLContext?.MakeCurrent();
        _gl.Clear(
            ClearBufferMask.ColorBufferBit
          | ClearBufferMask.DepthBufferBit
          | ClearBufferMask.StencilBufferBit
        );

        var bg = _appEngine.BackgroundColor;
        _skSurface.Canvas.Clear(new SKColor(
            (byte)(bg.R * 255),
            (byte)(bg.G * 255),
            (byte)(bg.B * 255),
            (byte)(bg.A * 255)
        ));

        _renderer.SetCanvas(_skSurface.Canvas, new Vector2(IWindow.Size.X, IWindow.Size.Y));
        _appEngine.UpdateAndRender(_renderer, _textService);
        _skSurface.Canvas.Flush();
        IWindow.SwapBuffers();
    }

    private void OnResize(Vector2D<int> size)
    {
        _gl?.Viewport(size);

        _renderTarget?.Dispose();
        _skSurface?.Dispose();

        _renderTarget = new GRBackendRenderTarget(
            size.X, size.Y, 0, 8,
            new GRGlFramebufferInfo(0, (uint)GLEnum.Rgba8)
        );

        _skSurface = SKSurface.Create(
            _grContext,
            _renderTarget,
            GRSurfaceOrigin.BottomLeft,
            SKColorType.Rgba8888
        );
    }

    private void OnClose() { }

    private void ApplyWindowStyles()
    {
        if (!OperatingSystem.IsWindows() || Handle == IntPtr.Zero)
            return;

        var backdrop = _owner.BackdropType;
        var theme = _owner.TitleBarTheme;
        bool modern = backdrop != WindowBackdropType.Default
                       && OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621);

        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763))
        {
            bool immersive = modern || theme != WindowTitleBarTheme.Default;
            if (immersive)
            {
                int dark = theme == WindowTitleBarTheme.Light ? 0 : 1;
                DwmApi.DwmSetWindowAttribute(
                    Handle,
                    DwmApi.DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE,
                    ref dark,
                    sizeof(int)
                );
            }
        }

        if (modern)
        {
            int val = backdrop switch
            {
                WindowBackdropType.Mica => (int)DwmApi.DWMSYSTEMBACKDROP_TYPE.DWMSBT_MAINWINDOW,
                WindowBackdropType.Acrylic => (int)DwmApi.DWMSYSTEMBACKDROP_TYPE.DWMSBT_TRANSIENTWINDOW,
                WindowBackdropType.Tabbed => (int)DwmApi.DWMSYSTEMBACKDROP_TYPE.DWMSBT_TABBEDWINDOW,
                _ => (int)DwmApi.DWMSYSTEMBACKDROP_TYPE.DWMSBT_AUTO,
            };

            if (backdrop == WindowBackdropType.Mica
                && theme == WindowTitleBarTheme.Light)
            {
                val = (int)DwmApi.DWMSYSTEMBACKDROP_TYPE.DWMSBT_TRANSIENTWINDOW;
            }

            DwmApi.DwmSetWindowAttribute(
                Handle,
                DwmApi.DWMWINDOWATTRIBUTE.DWMWA_SYSTEMBACKDROP_TYPE,
                ref val,
                sizeof(int)
            );
        }
    }

    #region Input Callbacks & Mapping

    private void OnKeyDown(IKeyboard kb, Key k, int s) => Input.AddKeyPressed(MapKey(k));
    private void OnKeyUp(IKeyboard kb, Key k, int s) => Input.AddKeyReleased(MapKey(k));
    private void OnKeyChar(IKeyboard kb, char c) => Input.AddCharacterInput(c);

    private void OnMouseDown(IMouse m, Silk.NET.Input.MouseButton b)
        => Input.SetMouseDown(MapMouseButton(b));
    private void OnMouseUp(IMouse m, Silk.NET.Input.MouseButton b)
        => Input.SetMouseUp(MapMouseButton(b));
    private void OnMouseMove(IMouse m, Vector2 pos)
        => Input.SetMousePosition((int)pos.X, (int)pos.Y);
    private void OnMouseWheel(IMouse m, ScrollWheel s)
        => Input.AddMouseWheelDelta(s.Y);

    // full MapKey/MapMouseButton omitted for brevity—you keep yours here unchanged

    #endregion

    public void Dispose()
    {
        if (_isDisposed) return;

        _appEngine?.Cleanup();
        _renderer?.Cleanup();
        _textService?.Cleanup();
        _skSurface?.Dispose();
        _renderTarget?.Dispose();
        _grContext?.Dispose();
        _gl?.Dispose();
        _inputContext?.Dispose();
        IWindow?.Dispose();

        _isDisposed = true;
        GC.SuppressFinalize(this);
    }

// inside SilkNetSkiaWindow class, below your OnMouseWheel handler:

/// <summary>
/// Maps a Silk.NET key into your DirectUI.Input.Keys enum.
/// </summary>
private static Keys MapKey(Key key) => key switch
{
    Key.Space => Keys.Space,
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

/// <summary>
/// Maps a Silk.NET mouse button into your DirectUI.MouseButton enum.
/// </summary>
private static DirectUI.MouseButton MapMouseButton(Silk.NET.Input.MouseButton button) => button switch
{
    Silk.NET.Input.MouseButton.Left => DirectUI.MouseButton.Left,
    Silk.NET.Input.MouseButton.Right => DirectUI.MouseButton.Right,
    Silk.NET.Input.MouseButton.Middle => DirectUI.MouseButton.Middle,
    Silk.NET.Input.MouseButton.Button4 => DirectUI.MouseButton.XButton1,
    Silk.NET.Input.MouseButton.Button5 => DirectUI.MouseButton.XButton2,
    _ => DirectUI.MouseButton.Left,
};

}