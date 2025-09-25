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

    private static int s_sdlInitCount = 0;
    private static int s_ttfInitCount = 0;

    private SDL3WindowHost? _activeModalWindow;
    private Action<UIContext>? _modalDrawCallback;
    private Action<int>? _onModalClosedCallback;
    private int _modalResultCode;
    private bool _isModalClosing;

    public AppEngine AppEngine => _appEngine ?? throw new InvalidOperationException("AppEngine is not initialized.");
    public IntPtr Handle => _windowPtr;
    public InputManager Input => _appEngine?.Input ?? new();
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

    public SDL3WindowHost(string title, int width, int initialHeight, Color4 backgroundColor)
    {
        _title = title;
        _initialWidth = width;
        _initialHeight = initialHeight;
        _backgroundColor = backgroundColor;
    }

    public bool Initialize(Action<UIContext> uiDrawCallback, Color4 backgroundColor, float initialScale = 1.0f)
    {
        Console.WriteLine($"SDL3WindowHost initializing for '{_title}'...");

        try
        {
            Interlocked.Increment(ref s_sdlInitCount);

            if (s_sdlInitCount == 1)
            {
                if (!SDL.Init(SDL.InitFlags.Video))
                {
                    Console.WriteLine($"SDL could not initialize! SDL_Error: {SDL.GetError()}");
                    return false;
                }
            }

            Interlocked.Increment(ref s_ttfInitCount);

            if (s_ttfInitCount == 1)
            {
                if (!TTF.Init())
                {
                    Console.WriteLine($"SDL_ttf could not initialize! SDL_Error: {SDL.GetError()}");
                    return false;
                }
                SDL3TextService.RegisterDefaultFonts();
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

            _appEngine = new(uiDrawCallback, backgroundColor);
            _appEngine.UIScale = initialScale;
            _renderer = new(_rendererPtr, _windowPtr);
            _textService = new();
            _appEngine.Initialize(_textService, _renderer);

            Console.WriteLine($"SDL3WindowHost '{_title}' initialized successfully. (SDL_Init count: {s_sdlInitCount}, TTF_Init count: {s_ttfInitCount})");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred during SDL3WindowHost initialization: {ex.Message}");
            Cleanup();
            return false;
        }
    }

    public void RunLoop()
    {
        bool running = true;

        while (running)
        {
            if (IsModalWindowOpen && _activeModalWindow is not null)
            {
                _activeModalWindow.ModalRunLoop();
                HandleModalLifecycle();
            }
            else
            {
                while (SDL.PollEvent(out SDL.Event ev))
                {
                    var keyModifiers = SDL.GetModState();
                    bool isCtrlDown = (keyModifiers & SDL.Keymod.Ctrl) != 0;

                    if (ev.Type == (uint)SDL.EventType.Quit)
                    {
                        running = false;
                        break;
                    }

                    if (ev.Type == (uint)SDL.EventType.MouseWheel && isCtrlDown && _appEngine is not null)
                    {
                        float deltaY = ev.Wheel.Y;
                        float scaleDelta = deltaY * 0.1f;
                        _appEngine.UIScale = Math.Clamp(_appEngine.UIScale + scaleDelta, 0.5f, 3.0f);
                    }
                    else
                    {
                        Input.ProcessSDL3Event(ev);
                    }
                }

                RenderFrame();
            }
        }
    }

    private void ModalRunLoop()
    {
        bool modalRunning = true;

        while (modalRunning)
        {
            while (SDL.PollEvent(out SDL.Event ev))
            {
                var keyModifiers = SDL.GetModState();
                bool isCtrlDown = (keyModifiers & SDL.Keymod.Ctrl) != 0;

                if (ev.Type == (uint)SDL.EventType.Quit)
                {
                    modalRunning = false;
                    _modalResultCode = -1;
                    break;
                }
                else if (ev.Type == (uint)SDL.EventType.WindowCloseRequested)
                {
                    if (ev.Window.WindowID == SDL.GetWindowID(_windowPtr))
                    {
                        modalRunning = false;
                        _modalResultCode = -1;
                        break;
                    }
                }

                if (ev.Type == (uint)SDL.EventType.MouseWheel && isCtrlDown && _appEngine is not null)
                {
                    float deltaY = ev.Wheel.Y;
                    float scaleDelta = deltaY * 0.1f;
                    _appEngine.UIScale = Math.Clamp(_appEngine.UIScale + scaleDelta, 0.5f, 3.0f);
                }
                else
                {
                    Input.ProcessSDL3Event(ev);
                }
            }

            if (_isModalClosing)
            {
                modalRunning = false;
            }

            ModalRenderFrame();
        }

        _isModalClosing = true;
    }

    private void RenderFrame()
    {
        SDL.SetRenderDrawColor(_rendererPtr, (byte)(_backgroundColor.R * 255), (byte)(_backgroundColor.G * 255), (byte)(_backgroundColor.B * 255), (byte)(_backgroundColor.A * 255));
        SDL.RenderClear(_rendererPtr);

        if (_renderer is not null && _textService is not null && _appEngine is not null)
        {
            _appEngine.UpdateAndRender(_renderer, _textService);
        }

        SDL.RenderPresent(_rendererPtr);
    }

    private void ModalRenderFrame()
    {
        SDL.SetRenderDrawColor(_rendererPtr, (byte)(_backgroundColor.R * 255), (byte)(_backgroundColor.G * 255), (byte)(_backgroundColor.B * 255), (byte)(_backgroundColor.A * 255));
        SDL.RenderClear(_rendererPtr);

        if (_renderer is not null && _textService is not null && _appEngine is not null && _modalDrawCallback is not null)
        {
            _appEngine.UpdateAndRender(_renderer, _textService);
        }

        SDL.RenderPresent(_rendererPtr);
    }

    public void Cleanup()
    {
        if (_isDisposed) return;
        Console.WriteLine($"SDL3WindowHost cleanup for '{_title}'...");

        _activeModalWindow?.Cleanup();

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

        Interlocked.Decrement(ref s_ttfInitCount);

        if (s_ttfInitCount == 0)
        {
            Console.WriteLine("Final TTF.Quit().");
            TTF.Quit();
        }

        Interlocked.Decrement(ref s_sdlInitCount);

        if (s_sdlInitCount == 0)
        {
            Console.WriteLine("Final SDL.Quit()..");
            SDL.Quit();
        }

        _isDisposed = true;

        GC.SuppressFinalize(this);
        Console.WriteLine($"SDL3WindowHost '{_title}' cleaned up. (SDL_Init count: {s_sdlInitCount}, TTF_Init count: {s_ttfInitCount})");
    }

    public void Dispose()
    {
        Cleanup();
    }

    public bool IsModalWindowOpen
    {
        get
        {
            return _activeModalWindow is not null;
        }
    }

    public void OpenModalWindow(string title, int width, int height, Action<UIContext> drawCallback, Action<int>? onClosedCallback = null)
    {
        if (_activeModalWindow is not null)
        {
            Console.WriteLine("Warning: Cannot open a new modal window while another is already active.");
            return;
        }

        _activeModalWindow = new SDL3WindowHost(title, width, height, _backgroundColor);
        _activeModalWindow._modalDrawCallback = drawCallback;
        _onModalClosedCallback = onClosedCallback;
        _modalResultCode = -1;

        if (!_activeModalWindow.InitializeModalInternal())
        {
            Console.WriteLine("Failed to initialize modal SDL window.");
            _activeModalWindow.Dispose();
            _activeModalWindow = null;
            onClosedCallback?.Invoke(-1);
            return;
        }

        if (!SDL.SetWindowParent(_activeModalWindow._windowPtr, _windowPtr))
        {
            Console.WriteLine($"Failed to set window parent: {SDL.GetError()}");
            _activeModalWindow.Dispose();
            _activeModalWindow = null;
            onClosedCallback?.Invoke(-1);
            return;
        }

        if (!SDL.SetWindowModal(_activeModalWindow._windowPtr, true))
        {
            Console.WriteLine($"Failed to set modal flag on window: {SDL.GetError()}");
            _activeModalWindow.Dispose();
            _activeModalWindow = null;
            onClosedCallback?.Invoke(-1);
            return;
        }

        Console.WriteLine("Modal window opened successfully.");
    }

    private bool InitializeModalInternal()
    {
        return Initialize(_modalDrawCallback!, _backgroundColor);
    }

    public void CloseModalWindow(int resultCode = 0)
    {
        if (_activeModalWindow is null)
        {
            return;
        }

        _activeModalWindow._modalResultCode = resultCode;
        _activeModalWindow._isModalClosing = true;
    }

    private void HandleModalLifecycle()
    {
        if (_activeModalWindow is null)
        {
            return;
        }

        if (!_activeModalWindow._isModalClosing)
        {
            return;
        }

        Console.WriteLine($"Modal window closed. Result: {_activeModalWindow._modalResultCode}");
        _onModalClosedCallback?.Invoke(_activeModalWindow._modalResultCode);

        if (_windowPtr != nint.Zero)
        {
            SDL.RaiseWindow(_windowPtr);
        }

        _activeModalWindow.Dispose();
        _activeModalWindow = null;
        _onModalClosedCallback = null;
        _modalResultCode = 0;
        _isModalClosing = false;
    }

    public bool Initialize(Action<UIContext> uiDrawCallback, Color4 backgroundColor)
    {
        throw new NotImplementedException();
    }
}