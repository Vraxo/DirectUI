using System;
using System.Diagnostics;
using System.Threading; // Added for Interlocked
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
    private readonly Color4 _backgroundColor; // Stored here to pass to new modal instances

    private nint _windowPtr;
    private nint _rendererPtr;

    private AppEngine? _appEngine;
    private SDL3Renderer? _renderer;
    private SDL3TextService? _textService;
    private bool _isDisposed;

    // Static counters for global SDL and TTF initialization/cleanup
    private static int s_sdlInitCount = 0;
    private static int s_ttfInitCount = 0;

    // Modal Window State
    private SDL3WindowHost? _activeModalWindow;
    private Action<UIContext>? _modalDrawCallback; // Only used by the modal instance itself
    private Action<int>? _onModalClosedCallback; // Used by the parent to get result from modal
    private int _modalResultCode;
    private bool _isModalClosing; // Flag to prevent re-entry during modal cleanup

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
            // Global SDL initialization (guarded by static counters)
            Interlocked.Increment(ref s_sdlInitCount);
            if (s_sdlInitCount == 1)
            {
                if (!SDL.Init(SDL.InitFlags.Video))
                {
                    Console.WriteLine($"SDL could not initialize! SDL_Error: {SDL.GetError()}");
                    return false;
                }
            }

            // Global SDL_ttf initialization (guarded by static counters)
            Interlocked.Increment(ref s_ttfInitCount);
            if (s_ttfInitCount == 1)
            {
                if (!TTF.Init())
                {
                    Console.WriteLine($"SDL_ttf could not initialize! SDL_Error: {SDL.GetError()}");
                    return false;
                }
                SDL3TextService.RegisterDefaultFonts(); // Register font paths once globally
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
                SDL.DestroyWindow(_windowPtr); // Clean up window if renderer fails
                return false;
            }

            // Create instance-specific DirectUI components
            _appEngine = new AppEngine(uiDrawCallback, backgroundColor);
            _renderer = new SDL3Renderer(_rendererPtr, _windowPtr);
            _textService = new SDL3TextService();
            _appEngine.Initialize(_textService, _renderer);

            Console.WriteLine($"SDL3WindowHost '{_title}' initialized successfully. (SDL_Init count: {s_sdlInitCount}, TTF_Init count: {s_ttfInitCount})");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred during SDL3WindowHost initialization: {ex.Message}");
            Cleanup(); // Ensure partial initialization is cleaned up
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
                // Delegate control to the modal window's own run loop
                _activeModalWindow.ModalRunLoop();
                // After modal loop finishes, handle its lifecycle (cleanup, callback)
                HandleModalLifecycle();
            }
            else
            {
                // Main window loop
                while (SDL.PollEvent(out SDL.Event ev))
                {
                    if (ev.Type == (uint)SDL.EventType.Quit)
                    {
                        running = false;
                        break;
                    }
                    // TODO: Handle main window close event if it's not WM_QUIT
                    Input.ProcessSDL3Event(ev); // Process input for the main window
                }

                this.RenderFrame(); // Explicitly call RenderFrame
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
                if (ev.Type == (uint)SDL.EventType.Quit)
                {
                    // This generally means the whole application is quitting
                    modalRunning = false;
                    _modalResultCode = -1; // Indicate a forced quit
                    break;
                }
                else if (ev.Type == (uint)SDL.EventType.WindowCloseRequested)
                {
                    // Check if the event is for THIS modal window and it's a close event
                    if (ev.Window.WindowID == SDL.GetWindowID(_windowPtr))
                    {
                        modalRunning = false; // Exit modal loop
                        _modalResultCode = -1; // Indicate closure by X button (cancel)
                        break;
                    }
                }
                // Process input for the modal window
                Input.ProcessSDL3Event(ev);
            }

            if (_isModalClosing)
            {
                modalRunning = false;
            }

            ModalRenderFrame();
        }
        // When modal loop exits, signal its cleanup to the parent
        _isModalClosing = true;
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

    private void ModalRenderFrame()
    {
        SDL.SetRenderDrawColor(_rendererPtr, (byte)(_backgroundColor.R * 255), (byte)(_backgroundColor.G * 255), (byte)(_backgroundColor.B * 255), (byte)(_backgroundColor.A * 255));
        SDL.RenderClear(_rendererPtr);

        if (_renderer != null && _textService != null && _appEngine != null && _modalDrawCallback != null)
        {
            // The modal's AppEngine will use its own _renderer and _textService
            _appEngine.UpdateAndRenderModal(_renderer, _textService, _modalDrawCallback);
        }

        SDL.RenderPresent(_rendererPtr);
    }

    public void Cleanup()
    {
        if (_isDisposed) return;
        Console.WriteLine($"SDL3WindowHost cleanup for '{_title}'...");

        // If this is a parent host and has an active modal, clean it up first
        _activeModalWindow?.Cleanup();

        // Clean up instance-specific DirectUI components
        _appEngine?.Cleanup();
        _renderer?.Cleanup();
        _textService?.Cleanup();

        // Destroy SDL renderer and window for THIS instance
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

        // Global SDL and TTF cleanup (guarded by static counters)
        Interlocked.Decrement(ref s_ttfInitCount);
        if (s_ttfInitCount == 0)
        {
            Console.WriteLine("Final TTF.Quit().");
            TTF.Quit();
        }
        Interlocked.Decrement(ref s_sdlInitCount);
        if (s_sdlInitCount == 0)
        {
            Console.WriteLine("Final SDL.Quit().");
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

    // --- IModalWindowService Implementation ---
    public bool IsModalWindowOpen => _activeModalWindow != null;

    public void OpenModalWindow(string title, int width, int height, Action<UIContext> drawCallback, Action<int>? onClosedCallback = null)
    {
        if (_activeModalWindow != null)
        {
            Console.WriteLine("Warning: Cannot open a new modal window while another is already active.");
            return;
        }

        // Create a NEW SDL3WindowHost instance for the modal window
        _activeModalWindow = new SDL3WindowHost(title, width, height, _backgroundColor);
        _activeModalWindow._modalDrawCallback = drawCallback; // This callback is for the modal's internal drawing logic
        _onModalClosedCallback = onClosedCallback;
        _modalResultCode = -1; // Default result

        // Initialize the modal window. This will create its own SDL window, renderer, and DirectUI components.
        // Important: this call creates the modal window, but without a parent specified in SDL_CreateWindow flags.
        if (!_activeModalWindow.InitializeModalInternal()) // Use internal helper that just calls Initialize()
        {
            Console.WriteLine("Failed to initialize modal SDL window.");
            _activeModalWindow.Dispose(); // Dispose failed modal
            _activeModalWindow = null;
            onClosedCallback?.Invoke(-1); // Indicate failure to the caller
            return;
        }

        // Now that _activeModalWindow._windowPtr is valid, set its parent.
        // This is crucial to satisfy the "Window must have a parent..." requirement for SDL.SetWindowModal().
        if (!SDL.SetWindowParent(_activeModalWindow._windowPtr, this._windowPtr))
        {
            Console.WriteLine($"Failed to set window parent: {SDL.GetError()}");
            _activeModalWindow.Dispose();
            _activeModalWindow = null;
            onClosedCallback?.Invoke(-1);
            return;
        }

        // Now that it has a parent, set it as modal.
        if (!SDL.SetWindowModal(_activeModalWindow._windowPtr, true)) // Assume 0 is success for SDL functions
        {
            Console.WriteLine($"Failed to set modal flag on window: {SDL.GetError()}");
            _activeModalWindow.Dispose();
            _activeModalWindow = null;
            onClosedCallback?.Invoke(-1);
            return;
        }
        Console.WriteLine("Modal window opened successfully.");
    }

    /// <summary>
    /// Internal helper to initialize the modal window's DirectUI components.
    /// This simply calls the main `Initialize` method with the modal's own draw callback.
    /// </summary>
    private bool InitializeModalInternal()
    {
        return Initialize(_modalDrawCallback!, _backgroundColor);
    }

    public void CloseModalWindow(int resultCode = 0)
    {
        if (_activeModalWindow != null)
        {
            _activeModalWindow._modalResultCode = resultCode;
            _activeModalWindow._isModalClosing = true; // Signal modal to close gracefully
        }
    }

    /// <summary>
    /// Manages the lifecycle of the modal window, checking its state each frame.
    /// </summary>
    private void HandleModalLifecycle()
    {
        if (_activeModalWindow == null) return;

        if (_activeModalWindow._isModalClosing)
        {
            Console.WriteLine($"Modal window closed. Result: {_activeModalWindow._modalResultCode}");
            _onModalClosedCallback?.Invoke(_activeModalWindow._modalResultCode); // Notify caller of closure and result

            // After modal closes, raise the parent window to ensure it gets focus back.
            // SDL automatically handles the modal un-setting when the modal window is destroyed.
            if (this._windowPtr != nint.Zero)
            {
                SDL.RaiseWindow(this._windowPtr);
            }

            _activeModalWindow.Dispose(); // Ensure all managed and unmanaged resources are cleaned up
            _activeModalWindow = null;
            _onModalClosedCallback = null;
            _modalResultCode = 0;
            _isModalClosing = false; // Reset flag
        }
    }
}