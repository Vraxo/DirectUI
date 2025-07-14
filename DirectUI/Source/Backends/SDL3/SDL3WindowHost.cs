using System;
using System.Threading;
using DirectUI.Core;
using DirectUI.Input;
using SDL;
using SDL.SDL3;
using SDL_ttf;
using Vortice.Mathematics;
using SizeI = Vortice.Mathematics.SizeI;

namespace DirectUI.Backends.SDL3;

public unsafe class SDL3WindowHost : IWindowHost, IModalWindowService, IDisposable
{
    private readonly string _title;
    private readonly int _initialWidth;
    private readonly int _initialHeight;
    private readonly Color4 _backgroundColor;

    private SDL_Window* _window;
    private SDL_Renderer* _renderer;

    private AppEngine? _engine;
    private SDL3Renderer? _gfx;
    private SDL3TextService? _text;
    private bool _disposed;

    private static int sdlInitCount = 0;
    private static int ttfInitCount = 0;

    private SDL3WindowHost? _modal;
    private Action<UIContext>? _modalDraw;
    private Action<int>? _modalClose;
    private int _modalResult;
    private bool _modalClosing;

    public IntPtr Handle => (IntPtr)_window;
    public InputManager Input => _engine?.Input ?? new();
    public SizeI ClientSize
    {
        get
        {
            SDL.GetWindowSize(_window, out int w, out int h);
            return new SizeI(w, h);
        }
    }

    public bool ShowFpsCounter
    {
        get => _engine?.ShowFpsCounter ?? false;
        set { if (_engine != null) _engine.ShowFpsCounter = value; }
    }

    public IModalWindowService ModalWindowService => this;

    public SDL3WindowHost(string title, int width, int height, Color4 background)
    {
        _title = title;
        _initialWidth = width;
        _initialHeight = height;
        _backgroundColor = background;
    }

    public bool Initialize(Action<UIContext> draw, Color4 background)
    {
        try
        {
            if (Interlocked.Increment(ref sdlInitCount) == 1)
            {
                if (!SDL.Init(InitFlags.Video))
                    throw new Exception($"SDL_Init failed: {SDL.GetError()}");
            }

            if (Interlocked.Increment(ref ttfInitCount) == 1)
            {
                if (!TTF.Init())
                    throw new Exception($"TTF_Init failed: {SDL.GetError()}");
                SDL3TextService.RegisterDefaultFonts();
            }

            _window = SDL.CreateWindow(
                _title,
                SDL.WindowPosCentered,
                SDL.WindowPosCentered,
                _initialWidth,
                _initialHeight,
                WindowFlags.Resizable
            );

            if (_window == null)
                throw new Exception($"Window creation failed: {SDL.GetError()}");

            _renderer = SDL.CreateRenderer(_window, -1, RendererFlags.Accelerated);

            if (_renderer == null)
                throw new Exception($"Renderer creation failed: {SDL.GetError()}");

            _engine = new(draw, background);
            _gfx = new SDL3Renderer(_renderer, _window);
            _text = new SDL3TextService();

            _engine.Initialize(_text, _gfx);

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Init failed: {ex.Message}");
            Cleanup();
            return false;
        }
    }

    public void RunLoop()
    {
        bool running = true;

        while (running)
        {
            if (IsModalWindowOpen && _modal != null)
            {
                _modal.ModalRunLoop();
                ProcessModalExit();
                continue;
            }

            while (SDL.PollEvent(out var ev) == 1)
            {
                if (ev.Type == EventType.Quit)
                {
                    running = false;
                    break;
                }

                Input.ProcessSDL3Event(ev);
            }

            RenderFrame();
        }
    }

    private void ModalRunLoop()
    {
        bool modalActive = true;

        while (modalActive)
        {
            SDL_Event @event;

            while (SDL.SDL3.SDL_PollEvent((SDL_Event*)@event) == 1)
            {
                if (ev.typ == EventType.Quit ||
                    (ev.Type == EventType.WindowEvent && ev.Window.WindowEventID == WindowEventID.Close))
                {
                    modalActive = false;
                    _modalResult = -1;
                    break;
                }

                Input.ProcessSDL3Event(ev);
            }

            if (_modalClosing)
                modalActive = false;

            RenderModalFrame();
        }

        _modalClosing = true;
    }

    private void RenderFrame()
    {
        SDL.SDL3.SDL_SetRenderDrawColor((SDL.SDL_Renderer*)_renderer,
            (byte)(_backgroundColor.R * 255),
            (byte)(_backgroundColor.G * 255),
            (byte)(_backgroundColor.B * 255),
            (byte)(_backgroundColor.A * 255));
        SDL.SDL3.SDL_RenderClear((SDL.SDL_Renderer*)_renderer);

        _engine?.UpdateAndRender(_gfx!, _text!);

        SDL.SDL3.SDL_RenderPresent((SDL.SDL_Renderer*)_renderer);
    }

    private void RenderModalFrame()
    {
        SDL.SDL3.SDL_SetRenderDrawColor((SDL.SDL_Renderer*)_renderer,
            (byte)(_backgroundColor.R * 255),
            (byte)(_backgroundColor.G * 255),
            (byte)(_backgroundColor.B * 255),
            (byte)(_backgroundColor.A * 255));
        SDL.SDL3.SDL_RenderClear((SDL.SDL_Renderer*)_renderer);

        _engine?.UpdateAndRenderModal(_gfx!, _text!, _modalDraw!);

        SDL.SDL3.SDL_RenderPresent((SDL.SDL_Renderer*)_renderer);
    }

    public void OpenModalWindow(string title, int width, int height, Action<UIContext> draw, Action<int>? onClose = null)
    {
        if (_modal != null)
        {
            Console.WriteLine("Modal window already open.");
            return;
        }

        _modal = new SDL3WindowHost(title, width, height, _backgroundColor)
        {
            _modalDraw = draw,
            _modalClose = onClose
        };

        if (!_modal.Initialize(draw, _backgroundColor))
        {
            Console.WriteLine("Failed to init modal.");
            _modal.Dispose();
            _modal = null;
            onClose?.Invoke(-1);
            return;
        }

        SDL.SDL3.SDL_SetWindowParent((SDL.SDL_Window*)_modal._window, (SDL.SDL_Window*)_window);
        SDL.SDL3.SDL_SetWindowModal((SDL.SDL_Window*)_modal._window, true);
    }

    public void CloseModalWindow(int result = 0)
    {
        if (_modal == null) return;
        _modal._modalResult = result;
        _modal._modalClosing = true;
    }

    private void ProcessModalExit()
    {
        if (_modal == null || !_modal._modalClosing)
            return;

        _modalClose?.Invoke(_modal._modalResult);

        SDL.SDL3.SDL_RaiseWindow((SDL.SDL_Window*)_window);
        _modal.Dispose();
        _modal = null;
        _modalClose = null;
        _modalClosing = false;
    }

    public void Cleanup()
    {
        if (_disposed) return;

        _modal?.Cleanup();
        _engine?.Cleanup();
        _gfx?.Cleanup();
        _text?.Cleanup();

        if (_renderer != null)
            SDL.SDL3.SDL_DestroyRenderer((SDL.SDL_Renderer*)_renderer);

        if (_window != null)
            SDL.SDL3.SDL_DestroyWindow((SDL.SDL_Window*)_window);

        if (Interlocked.Decrement(ref ttfInitCount) == 0)
            SDL.SDL3_ttf.TTF_Quit();

        if (Interlocked.Decrement(ref sdlInitCount) == 0)
            SDL.SDL3.SDL_Quit();

        _disposed = true;
    }

    public void Dispose() => Cleanup();

    public bool IsModalWindowOpen => _modal != null;
}
