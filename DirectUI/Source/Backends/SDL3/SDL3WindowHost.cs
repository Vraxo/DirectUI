using System;
using System.Diagnostics;
using DirectUI.Core;
using DirectUI.Input;
using SDL3;
using Vortice.Mathematics;
using SizeI = Vortice.Mathematics.SizeI;

namespace DirectUI.Backends.SDL3;

public unsafe class SDL3WindowHost : IWindowHost, IModalWindowService
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
    private bool _isDisposed;

    private SDL3WindowHost? _activeModalWindow;
    private Action<UIContext>? _modalDrawCallback;
    private Action<int>? _onModalClosedCallback;
    private int _modalResultCode;
    private bool _isModalClosing;

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

    public IModalWindowService ModalWindowService => this;

    public SDL3WindowHost(string title, int width, int height, Color4 backgroundColor)
    {
        _title = title;
        _initialWidth = width;
        _initialHeight = height;
        _backgroundColor = backgroundColor;
    }

    public bool Initialize(Action<UIContext> uiDrawCallback, Color4 backgroundColor)
    {
        try
        {
            if (!SDL.Init(SDL.InitFlags.Video))
            {
                Console.WriteLine($"SDL could not initialize! SDL_Error: {SDL.GetError()}");
                return false;
            }

            if (!TTF.Init())
            {
                return false;
            }

            _windowPtr = SDL.CreateWindow(_title, _initialWidth, _initialHeight, SDL.WindowFlags.Resizable);
            
            if (_windowPtr == nint.Zero)
            {
                Console.WriteLine($"Window could not be created! SDL_Error: {SDL.GetError()}");
                return false;
            }

            _rendererPtr = SDL.CreateRenderer(_windowPtr, null);
            if (_rendererPtr == nint.Zero)
            {
                Console.WriteLine($"Renderer could not be created! SDL Error: {SDL.GetError()}");
                SDL.DestroyWindow(_windowPtr);
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
            Console.WriteLine($"An error occurred during initialization: {ex.Message}");
            Cleanup();
            return false;
        }
    }

    public void RunLoop()
    {
        bool running = true;
        while (running)
        {
            if (IsModalWindowOpen && _activeModalWindow != null)
            {
                _activeModalWindow.ModalRunLoop();
                HandleModalLifecycle();
            }
            else
            {
                while (SDL.PollEvent(out SDL.Event ev))
                {
                    if (ev.Type == (uint)SDL.EventType.Quit)
                    {
                        running = false;
                        break;
                    }
                    Input.ProcessSDL3Event(ev);
                }
                RenderFrame();
            }
        }
    }

    private void RenderFrame()
    {
        SDL.SetRenderDrawColor(_rendererPtr, (byte)(_backgroundColor.R * 255), (byte)(_backgroundColor.G * 255), (byte)(_backgroundColor.B * 255), (byte)(_backgroundColor.A * 255));
        SDL.RenderClear(_rendererPtr);

        if (_renderer != null && _textService != null && _appEngine != null)
        {
            _appEngine.UpdateAndRender(_renderer, _textService);
        }

        SDL.RenderPresent(_rendererPtr);
    }

    private void ModalRunLoop()
    {
        bool modalRunning = true;
        while (modalRunning)
        {
            while (SDL.PollEvent(out SDL.Event ev))
            {
                if (ev.Type == (uint)SDL.EventType.Quit)
                {
                    modalRunning = false;
                    _modalResultCode = -1;
                    break;
                }
                Input.ProcessSDL3Event(ev);
            }

            if (_isModalClosing)
            {
                modalRunning = false;
            }

            ModalRenderFrame();
        }
        _isModalClosing = true;
    }

    private void ModalRenderFrame()
    {
        SDL.SetRenderDrawColor(_rendererPtr, (byte)(_backgroundColor.R * 255), (byte)(_backgroundColor.G * 255), (byte)(_backgroundColor.B * 255), (byte)(_backgroundColor.A * 255));
        SDL.RenderClear(_rendererPtr);

        if (_renderer != null && _textService != null && _appEngine != null && _modalDrawCallback != null)
        {
            _appEngine.UpdateAndRenderModal(_renderer, _textService, _modalDrawCallback);
        }

        SDL.RenderPresent(_rendererPtr);
    }

    public void Cleanup()
    {
        if (_isDisposed) return;

        _activeModalWindow?.Cleanup();
        _renderer?.Cleanup();
        _textService?.Cleanup();
        _appEngine?.Cleanup();

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

        TTF.Quit();
        SDL.Quit();

        _isDisposed = true;
        GC.SuppressFinalize(this);
    }

    public void Dispose()
    {
        Cleanup();
    }

    public bool IsModalWindowOpen => _activeModalWindow != null;

    public void OpenModalWindow(string title, int width, int height, Action<UIContext> drawCallback, Action<int>? onClosedCallback = null)
    {
        if (_activeModalWindow != null)
        {
            Console.WriteLine("Warning: Cannot open a new modal window while another is already active.");
            return;
        }

        _activeModalWindow = new SDL3WindowHost(title, width, height, _backgroundColor)
        {
            _appEngine = this._appEngine,
            _textService = this._textService,
            _modalDrawCallback = drawCallback
        };

        _onModalClosedCallback = onClosedCallback;
        _modalResultCode = -1;

        if (!_activeModalWindow.InitializeModal(this))
        {
            _activeModalWindow.Dispose();
            _activeModalWindow = null;
            onClosedCallback?.Invoke(-1);
        }
    }

    private bool InitializeModal(SDL3WindowHost parent)
    {
        _windowPtr = SDL.CreateWindow(this._title, this._initialWidth, this._initialHeight, SDL.WindowFlags.Modal);
        if (_windowPtr == nint.Zero)
        {
            Console.WriteLine($"Failed to create modal SDL window: {SDL.GetError()}");
            return false;
        }

        _rendererPtr = SDL.CreateRenderer(_windowPtr, null);
        if (_rendererPtr == nint.Zero)
        {
            Console.WriteLine($"Failed to create modal SDL renderer: {SDL.GetError()}");
            SDL.DestroyWindow(_windowPtr);
            return false;
        }

        _renderer = new SDL3Renderer(_rendererPtr, _windowPtr);
        return true;
    }

    public void CloseModalWindow(int resultCode = 0)
    {
        if (_activeModalWindow != null)
        {
            _activeModalWindow._modalResultCode = resultCode;
            _activeModalWindow._isModalClosing = true;
        }
    }

    private void HandleModalLifecycle()
    {
        if (_activeModalWindow?._isModalClosing == true)
        {
            _onModalClosedCallback?.Invoke(_activeModalWindow._modalResultCode);
            _activeModalWindow.Dispose();
            _activeModalWindow = null;
            _onModalClosedCallback = null;
        }
    }
}
