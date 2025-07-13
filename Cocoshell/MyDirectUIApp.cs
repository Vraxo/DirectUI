// MyDesktopAppWindow.cs (Renamed from MyDirectUIApp.cs)
using System;
using System.Numerics;
using Vortice.Mathematics;
using Raylib_cs; // Added for Raylib

namespace DirectUI;

/// <summary>
/// This class serves as the concrete Win32 window for Direct2D and Raylib backends.
/// It wraps the core UI logic provided by MyUILogic.
/// </summary>
public class MyDesktopAppWindow : Direct2DAppWindow // Still inherits for D2D/Raylib
{
    private readonly MyUILogic _uiLogic;
    private readonly GraphicsBackend _backend;

    public MyDesktopAppWindow(string title, int width, int height, GraphicsBackend backend)
        : base(title, width, height)
    {
        _backend = backend;
        _uiLogic = new MyUILogic(backend); // Instantiate the UI logic
        // Provide a lambda for the UI logic to call to open the project window,
        // passing 'this' (the Win32Window instance) as the owner.
        _uiLogic.SetOpenProjectWindowHostAction(() => _uiLogic.OpenProjectWindowInternal(this));
    }

    /// <summary>
    /// Creates the window and initializes resources for either D2D or Raylib backend.
    /// This is the primary entry point for starting the application window.
    /// </summary>
    public bool CreateHostWindow()
    {
        if (_backend is GraphicsBackend.Raylib or GraphicsBackend.SDL3)
        {
            // For Raylib/Vulkan/SDL3, we bypass the Win32 window creation here.
            // SDL3 and Vulkan are handled by ApplicationRunner directly.
            // Raylib creates its window within AppHost.Initialize, so we just need to call Initialize.
            return Initialize();
        }
        else
        {
            // For D2D, use the standard Win32 window creation from the base class.
            return Create();
        }
    }

    // Override Initialize and Cleanup in Win32Window base if using Raylib
    protected override bool Initialize()
    {
        switch (_backend)
        {
            case GraphicsBackend.Raylib:
                Console.WriteLine("Initializing with Raylib backend...");
                Raylib.SetConfigFlags(ConfigFlags.Msaa4xHint);
                Raylib.InitWindow(Width, Height, Title);
                Raylib.SetTargetFPS(60);
                _appHost = CreateAppHost();
                return _appHost.Initialize(IntPtr.Zero, new Vortice.Mathematics.SizeI(Width, Height));

            case GraphicsBackend.SDL3:
                // This logic is now handled by ApplicationRunner and their respective hosts.
                // This MyDesktopAppWindow should not be instantiated for these backends.
                Console.WriteLine($"Error: MyDesktopAppWindow should not be used with {_backend} backend.");
                return false;

            case GraphicsBackend.Direct2D:
            default:
                return base.Initialize();
        }
    }

    protected override void Cleanup()
    {
        switch (_backend)
        {
            case GraphicsBackend.Raylib:
                Console.WriteLine("RaylibAppWindow cleaning up its resources...");
                _appHost?.Cleanup();
                _appHost = null;
                Raylib.CloseWindow();
                break;

            case GraphicsBackend.SDL3:
                // Nothing to do here, ApplicationRunner/Host handles cleanup for these.
                break;

            case GraphicsBackend.Direct2D:
            default:
                base.Cleanup();
                break;
        }
    }

    // For Raylib, the main loop in Program.cs handles drawing, not OnPaint messages.
    // OnPaint will only be called for D2D backend.
    protected override void OnPaint()
    {
        if (_backend == GraphicsBackend.Direct2D)
        {
            _appHost?.Render();
        }
    }

    public override void FrameUpdate()
    {
        switch (_backend)
        {
            case GraphicsBackend.Raylib:
                if (Raylib.WindowShouldClose())
                {
                    Application.Exit();
                    return;
                }
                _appHost?.Input.ProcessRaylibInput();
                _appHost?.Render();
                break;

            case GraphicsBackend.SDL3:
                // This method should not be called for Veldrid/SDL3 backends, as their loop is in ApplicationRunner.
                break;

            case GraphicsBackend.Direct2D:
            default:
                // For D2D, Invalidate is enough to trigger WM_PAINT for continuous loop.
                Invalidate();
                break;
        }
    }

    protected override AppHost CreateAppHost()
    {
        var backgroundColor = new Color4(21 / 255f, 21 / 255f, 21 / 255f, 1.0f); // #151515
        // Pass the DrawUI method from the _uiLogic instance.
        return new AppHost(_uiLogic.DrawUI, backgroundColor, _backend == GraphicsBackend.Raylib);
    }

    // Input handlers only call base if D2D backend, because Raylib's input is handled directly by AppHost.
    protected override void OnKeyDown(Keys key)
    {
        if (_backend == GraphicsBackend.Direct2D) base.OnKeyDown(key);
        if (key == Keys.F3)
        {
            if (_appHost != null) _appHost.ShowFpsCounter = !_appHost.ShowFpsCounter;
        }
    }

    protected override void OnMouseMove(int x, int y)
    {
        if (_backend == GraphicsBackend.Direct2D) base.OnMouseMove(x, y);
    }

    protected override void OnMouseDown(MouseButton button, int x, int y)
    {
        if (_backend == GraphicsBackend.Direct2D) base.OnMouseDown(button, x, y);
    }

    protected override void OnMouseUp(MouseButton button, int x, int y)
    {
        if (_backend == GraphicsBackend.Direct2D) base.OnMouseUp(button, x, y);
    }

    protected override void OnMouseWheel(float delta)
    {
        if (_backend == GraphicsBackend.Direct2D) base.OnMouseWheel(delta);
    }

    protected override void OnKeyUp(Keys key)
    {
        if (_backend == GraphicsBackend.Direct2D) base.OnKeyUp(key);
    }

    protected override void OnChar(char c)
    {
        if (_backend == GraphicsBackend.Direct2D) base.OnChar(c);
    }
}