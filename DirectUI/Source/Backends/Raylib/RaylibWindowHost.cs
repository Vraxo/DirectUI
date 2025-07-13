// DirectUI/RaylibWindowHost.cs
using System;
using System.Numerics;
using DirectUI.Core; // For IWindowHost, IModalWindowService
using DirectUI.Drawing;
using DirectUI.Input;
using Raylib_cs;
using Vortice.Mathematics;
using SizeI = Vortice.Mathematics.SizeI;

namespace DirectUI;

/// <summary>
/// A concrete implementation of <see cref="IWindowHost"/> for the Raylib backend.
/// </summary>
public class RaylibWindowHost : IWindowHost
{
    private readonly string _title;
    private readonly int _width;
    private readonly int _height;
    private readonly Color4 _backgroundColor;

    private AppEngine? _appEngine;
    private IRenderer? _renderer;
    private ITextService? _textService;
    private bool _isDisposed = false;

    // Raylib doesn't have a native window handle like Win32 or SDL, so this will be IntPtr.Zero.
    public IntPtr Handle => IntPtr.Zero;
    public InputManager Input => _appEngine?.Input ?? new InputManager();
    public SizeI ClientSize => new SizeI(Raylib.GetScreenWidth(), Raylib.GetScreenHeight());

    public bool ShowFpsCounter
    {
        get => _appEngine?.ShowFpsCounter ?? false;
        set { if (_appEngine != null) _appEngine.ShowFpsCounter = value; }
    }

    public IModalWindowService ModalWindowService { get; } = new RaylibDummyModalWindowService();

    public RaylibWindowHost(string title, int width, int height, Color4 backgroundColor)
    {
        _title = title;
        _width = width;
        _height = height;
        _backgroundColor = backgroundColor;
    }

    public bool Initialize(Action<UIContext> uiDrawCallback, Color4 backgroundColor)
    {
        Console.WriteLine("RaylibWindowHost initializing...");
        try
        {
            Raylib.SetConfigFlags(ConfigFlags.Msaa4xHint);
            Raylib.InitWindow(_width, _height, _title);
            Raylib.SetTargetFPS(60);

            _appEngine = new AppEngine(uiDrawCallback, backgroundColor);
            _renderer = new DirectUI.Backends.RaylibRenderer();
            _textService = new DirectUI.Backends.RaylibTextService();

            // Raylib-specific font initialization and registration
            FontManager.Initialize();
            FontManager.RegisterFontVariant("Segoe UI", Vortice.DirectWrite.FontWeight.Normal, "C:/Windows/Fonts/segoeui.ttf");
            FontManager.RegisterFontVariant("Segoe UI", Vortice.DirectWrite.FontWeight.SemiBold, "C:/Windows/Fonts/seguisb.ttf");
            FontManager.RegisterFontVariant("Consolas", Vortice.DirectWrite.FontWeight.Normal, "C:/Windows/Fonts/consola.ttf");
            FontManager.RegisterFontVariant("Consolas", Vortice.DirectWrite.FontWeight.Bold, "C:/Windows/Fonts/consolab.ttf");

            _appEngine.Initialize(_textService, _renderer);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to initialize RaylibWindowHost: {ex.Message}");
            Cleanup(); // Ensure partial initialization is cleaned up
            return false;
        }
    }

    public void RunLoop()
    {
        while (!Raylib.WindowShouldClose())
        {
            // Process Raylib input events directly
            _appEngine?.Input.ProcessRaylibInput();

            // Begin drawing
            Raylib.BeginDrawing();
            Raylib.ClearBackground(new Raylib_cs.Color(
                (byte)(_backgroundColor.R * 255),
                (byte)(_backgroundColor.G * 255),
                (byte)(_backgroundColor.B * 255),
                (byte)(_backgroundColor.A * 255)
            ));

            // Update and render the UI through the AppEngine
            if (_renderer is not null && _textService is not null && _appEngine is not null)
            {
                _appEngine.UpdateAndRender(_renderer, _textService);
            }

            // End drawing
            Raylib.EndDrawing();
        }
    }

    public void Cleanup()
    {
        if (_isDisposed) return;
        Console.WriteLine("RaylibWindowHost cleaning up its resources...");
        _appEngine?.Cleanup();
        (_renderer as DirectUI.Backends.RaylibRenderer)?.Cleanup(); // Explicit cast for Cleanup
        (_textService as DirectUI.Backends.RaylibTextService)?.Cleanup(); // Explicit cast for Cleanup
        FontManager.UnloadAll(); // Unload all Raylib fonts

        if (Raylib.IsWindowReady())
        {
            Raylib.CloseWindow();
        }
        _appEngine = null;
        _renderer = null;
        _textService = null;
        _isDisposed = true;
        GC.SuppressFinalize(this);
    }

    public void Dispose()
    {
        Cleanup();
    }

    private class RaylibDummyModalWindowService : IModalWindowService
    {
        public bool IsModalWindowOpen => false;

        public void OpenModalWindow(string title, int width, int height, Action<UIContext> drawCallback, Action<int>? onClosedCallback = null)
        {
            Console.WriteLine($"Modal window '{title}' requested but not supported in Raylib backend.");
            onClosedCallback?.Invoke(-1); // Immediately report as closed/failed
        }

        public void CloseModalWindow(int resultCode = 0)
        {
            // Do nothing as no modal windows are opened.
        }
    }
}