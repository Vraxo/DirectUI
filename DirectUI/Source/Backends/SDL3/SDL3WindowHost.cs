using System;
using System.Diagnostics;
using DirectUI.Core; // For IWindowHost, IModalWindowService
using DirectUI.Diagnostics;
using DirectUI.Input;
using SDL3;
using Vortice.Mathematics;
using SizeI = Vortice.Mathematics.SizeI;

namespace DirectUI.Backends.SDL3;

/// <summary>
/// A concrete implementation of <see cref="IWindowHost"/> for the SDL3 backend.
/// </summary>
public unsafe class SDL3WindowHost : IWindowHost
{
    private readonly string _title;
    private readonly int _initialWidth;
    private readonly int _initialHeight;
    private readonly Color4 _backgroundColor;

    private nint _windowPtr;
    private nint _rendererPtr;

    private AppEngine? _appEngine;
    private SDL3Renderer? _renderer;
    private SDL3TextService? _textService;
    private bool _isDisposed = false;

    // SDL3 doesn't typically provide a standard "handle" like Win32's HWND,
    // but the nint window pointer can serve a similar purpose for internal SDL calls.
    public IntPtr Handle => _windowPtr;
    public InputManager Input => _appEngine?.Input ?? new InputManager();
    public SizeI ClientSize
    {
        get
        {
            SDL.GetWindowSize(_windowPtr, out int w, out int h);
            return new SizeI(w, h);
        }
    }

    public bool ShowFpsCounter
    {
        get => _appEngine?.ShowFpsCounter ?? false;
        set { if (_appEngine != null) _appEngine.ShowFpsCounter = value; }
    }

    public IModalWindowService ModalWindowService { get; } = new SDL3DummyModalWindowService();

    public SDL3WindowHost(string title, int width, int height, Color4 backgroundColor)
    {
        _title = title;
        _initialWidth = width;
        _initialHeight = height;
        _backgroundColor = backgroundColor;
    }

    public bool Initialize(Action<UIContext> uiDrawCallback, Color4 backgroundColor)
    {
        Console.WriteLine("SDL3WindowHost initializing...");
        try
        {
            SDL.Init(SDL.InitFlags.Video);
            TTF.Init(); // Initialize SDL_ttf

            _windowPtr = SDL.CreateWindow(_title, _initialWidth, _initialHeight, SDL.WindowFlags.Resizable);
            if (_windowPtr == nint.Zero)
            {
                Console.WriteLine($"Failed to create SDL window: {SDL.GetError()}");
                return false;
            }

            _rendererPtr = SDL.CreateRenderer(_windowPtr, null);
            if (_rendererPtr == nint.Zero)
            {
                Console.WriteLine($"Failed to create SDL renderer: {SDL.GetError()}");
                SDL.DestroyWindow(_windowPtr);
                _windowPtr = nint.Zero;
                return false;
            }

            _appEngine = new AppEngine(uiDrawCallback, backgroundColor);
            _renderer = new SDL3Renderer(_rendererPtr, _windowPtr);
            _textService = new SDL3TextService();

            _appEngine.Initialize(_textService, _renderer);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to initialize SDL3WindowHost: {ex.Message}");
            Cleanup();
            return false;
        }
    }

    public void RunLoop()
    {
        bool running = true;
        while (running)
        {
            while (SDL.PollEvent(out SDL.Event ev))
            {
                Input.ProcessSDL3Event(ev); // Process input events through the AppEngine's InputManager

                if ((SDL.EventType)ev.Type == SDL.EventType.Quit)
                {
                    running = false;
                }
                else if ((SDL.EventType)ev.Type == SDL.EventType.WindowResized || (SDL.EventType)ev.Type == SDL.EventType.WindowPixelSizeChanged)
                {
                    SDL.GetWindowSize(_windowPtr, out int newWidth, out int newHeight);
                    _renderer?.UpdateWindowSize(newWidth, newHeight);
                }
            }

            // Clear the renderer buffer
            SDL.SetRenderDrawColor(_rendererPtr, (byte)(_backgroundColor.R * 255), (byte)(_backgroundColor.G * 255), (byte)(_backgroundColor.B * 255), (byte)(_backgroundColor.A * 255));
            SDL.RenderClear(_rendererPtr);

            // Update and render the UI through the AppEngine
            if (_renderer is not null && _textService is not null && _appEngine is not null)
            {
                _appEngine.UpdateAndRender(_renderer, _textService);
                _renderer.Flush(); // Flush batched commands, if any
            }

            SDL.RenderPresent(_rendererPtr); // Present the rendered frame
        }
    }

    public void Cleanup()
    {
        if (_isDisposed) return;
        Console.WriteLine("SDL3WindowHost cleaning up its resources...");
        _appEngine?.Cleanup();
        _renderer?.Cleanup();
        _textService?.Cleanup();

        if (_rendererPtr != nint.Zero)
        {
            SDL.DestroyRenderer(_rendererPtr);
            _rendererPtr = nint.Zero;
        }
        if (_windowPtr != nint.Zero)
        {
            SDL.DestroyWindow(_windowPtr);
            _windowPtr = nint.Zero;
        }

        TTF.Quit(); // Quit SDL_ttf after all font resources are cleaned up
        SDL.Quit();

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

    private class SDL3DummyModalWindowService : IModalWindowService
    {
        public bool IsModalWindowOpen => false;

        public void OpenModalWindow(string title, int width, int height, Action<UIContext> drawCallback, Action<int>? onClosedCallback = null)
        {
            Console.WriteLine($"Modal window '{title}' requested but not supported in SDL3 backend.");
            onClosedCallback?.Invoke(-1); // Immediately report as closed/failed
        }

        public void CloseModalWindow(int resultCode = 0)
        {
            // Do nothing as no modal windows are opened.
        }
    }
}
