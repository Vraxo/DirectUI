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

    // State for managing overlay modals
    private bool _isModalActive;
    private Rect _currentModalBounds; // Bounds of the modal overlay relative to the main window
    private Action<UIContext>? _currentModalDrawCallback;
    private Action<int>? _currentOnModalClosedCallback;
    private int _currentModalResultCode;

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

    public bool Initialize(Action<UIContext> uiDrawCallback, Color4 backgroundColor)
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
            _renderer = new(_rendererPtr, _windowPtr);
            _textService = new();
            _appEngine.Initialize(_textService, _renderer);

            _isModalActive = false;
            _currentModalDrawCallback = null;
            _currentOnModalClosedCallback = null;
            _currentModalResultCode = 0;
            _currentModalBounds = default;

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
            while (SDL.PollEvent(out SDL.Event ev))
            {
                if (ev.Type == (uint)SDL.EventType.Quit)
                {
                    running = false;
                    break;
                }
                else if (ev.Type == (uint)SDL.EventType.WindowCloseRequested)
                {
                    if (ev.Window.WindowID == SDL.GetWindowID(_windowPtr))
                    {
                        if (_isModalActive)
                        {
                            CloseModalWindow(-1); // Close modal if main window X-button is pressed
                        }
                        else
                        {
                            running = false; // Close main loop
                            break;
                        }
                    }
                }

                Input.ProcessSDL3Event(ev);
            }

            RenderFrame();
        }
    }

    private void RenderFrame()
    {
        SDL.SetRenderDrawColor(_rendererPtr, (byte)(_backgroundColor.R * 255), (byte)(_backgroundColor.G * 255), (byte)(_backgroundColor.B * 255), (byte)(_backgroundColor.A * 255));
        SDL.RenderClear(_rendererPtr);

        if (_renderer is not null && _textService is not null && _appEngine is not null)
        {
            if (_isModalActive)
            {
                // Draw a dimming overlay first, covering the entire window
                _renderer.DrawBox(
                    new Rect(0, 0, _renderer.RenderTargetSize.X, _renderer.RenderTargetSize.Y),
                    new BoxStyle { FillColor = new Color4(0, 0, 0, 0.5f), Roundness = 0f, BorderLength = 0f });

                // Then, draw the modal's content by providing its specific draw callback to the AppEngine
                // This call effectively replaces the main UI rendering for this frame.
                _appEngine.UpdateAndRenderModal(_renderer, _textService,
                    (ctx) =>
                    {
                        // Push a layout origin for the modal's internal UI elements
                        // to draw relative to 0,0 for its content area.
                        ctx.Layout.PushLayoutOrigin(_currentModalBounds.TopLeft);

                        // Push a clip rect for the modal's content area
                        ctx.Renderer.PushClipRect(_currentModalBounds);
                        ctx.Layout.PushClipRect(_currentModalBounds);

                        _currentModalDrawCallback?.Invoke(ctx);

                        // Pop clip rect and layout origin
                        ctx.Layout.PopClipRect();
                        ctx.Renderer.PopClipRect();
                        ctx.Layout.PopLayoutOrigin();
                    }
                );
            }
            else
            {
                // If no modal is active, draw the main UI
                _appEngine.UpdateAndRender(_renderer, _textService);
            }
        }

        SDL.RenderPresent(_rendererPtr);
    }

    public void Cleanup()
    {
        if (_isDisposed) return;
        Console.WriteLine($"SDL3WindowHost cleanup for '{_title}'...");

        // Ensure the modal state is reset if the main window is destroyed while a modal is active.
        if (_isModalActive)
        {
            _isModalActive = false;
            _currentModalDrawCallback = null;
            _currentOnModalClosedCallback = null;
            _currentModalBounds = default;
        }

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

    public bool IsModalWindowOpen => _isModalActive;

    public void OpenModalWindow(string title, int width, int height, Action<UIContext> drawCallback, Action<int>? onClosedCallback = null)
    {
        if (_isModalActive)
        {
            Console.WriteLine("Warning: Cannot open a new modal window while another is already active.");
            onClosedCallback?.Invoke(-1);
            return;
        }

        // Calculate modal position to center it over the main window
        var clientSize = ClientSize;
        float modalX = (clientSize.Width - width) / 2f;
        float modalY = (clientSize.Height - height) / 2f;
        _currentModalBounds = new Rect(modalX, modalY, width, height);

        _currentModalDrawCallback = drawCallback;
        _currentOnModalClosedCallback = onClosedCallback;
        _currentModalResultCode = -1; // Default to cancel/error

        _isModalActive = true;
        // SDL does not have a direct equivalent to EnableWindow(hwnd, false) for blocking interaction.
        // The modal overlay and the fact that we render only the modal will achieve the blocking visually.
    }

    public void CloseModalWindow(int resultCode = 0)
    {
        if (!_isModalActive)
        {
            return;
        }

        _currentModalResultCode = resultCode;
        _currentOnModalClosedCallback?.Invoke(_currentModalResultCode);

        // Clear local modal state
        _isModalActive = false;
        _currentModalDrawCallback = null;
        _currentOnModalClosedCallback = null;
        _currentModalBounds = default;
    }
}