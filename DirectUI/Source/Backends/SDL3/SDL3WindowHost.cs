using DirectUI.Core;
using DirectUI.Input;
using SDL3;
using Vortice.Mathematics;
using static SDL3.SDL;
using SizeI = Vortice.Mathematics.SizeI;

namespace DirectUI.Backends.SDL3;

public unsafe class SDL3WindowHost : IWindowHost, IModalWindowService
{
    private readonly string _title;
    private readonly int _initialWidth;
    private readonly int _initialHeight;
    private readonly Color4 _backgroundColor;

    private nint _windowPtr;
    private nint _gpuDevice;
    private nint _depthTexture;

    private AppEngine? _appEngine;
    private SDL3Renderer? _renderer;
    private SDL3TextService? _textService;
    private CubeRenderer? _cubeRenderer;
    private bool _isDisposed;

    private static int s_sdlInitCount = 0;
    private static int s_ttfInitCount = 0;

    private SDL3WindowHost? _activeModalWindow;
    private Action<UIContext>? _modalDrawCallback;
    private Action<int>? _onModalClosedCallback;
    private int _modalResultCode;
    private bool _isModalClosing;

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

            _windowPtr = SDL.CreateWindow(_title, _initialWidth, _initialHeight, SDL.WindowFlags.Resizable | SDL.WindowFlags.Vulkan);

            if (_windowPtr == nint.Zero)
            {
                Console.WriteLine($"Window could not be created! SDL_Error: {SDL.GetError()}");
                return false;
            }

            _gpuDevice = SDL.CreateGPUDevice(GPUShaderFormat.SPIRV, true, "Hi"); ;

            if (_gpuDevice == null)
            {
                Console.WriteLine($"GPU Device could not be created! SDL Error: {SDL.GetError()}");
                SDL.DestroyWindow(_windowPtr);
                return false;
            }

            CreateDepthBuffer();

            _appEngine = new(uiDrawCallback, backgroundColor);
            _renderer = new(_gpuDevice, _windowPtr);
            _textService = new();
            _cubeRenderer = new CubeRenderer(_gpuDevice);

            if (!_cubeRenderer.Initialize())
            {
                Console.WriteLine("Could not initialize CubeRenderer.");
                return false;
            }

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
                    if (ev.Type == (uint)SDL.EventType.Quit)
                    {
                        running = false;
                        break;
                    }
                    else if (ev.Type == (uint)SDL.EventType.WindowResized)
                    {
                        CreateDepthBuffer();
                    }


                    Input.ProcessSDL3Event(ev);
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

    private void RenderFrame()
    {
        nint cmdbuf = SDL.AcquireGPUCommandBuffer((nint)_gpuDevice);

        if (!SDL.AcquireGPUSwapchainTexture((nint)cmdbuf, _windowPtr, out nint t, out uint w, out uint h))
        {
            var colorAttachment = new SDL.GPUColorTargetInfo
            {
                //Texture = SDL.WaitAndAcquireGPUSwapchainTexture
                ClearColor = new SDL.FColor { R = (byte)_backgroundColor.R, G = (byte)_backgroundColor.G, B = (byte)_backgroundColor.B, A = (byte)_backgroundColor.A },
                LoadOp = GPULoadOp.Clear,
                StoreOp = GPUStoreOp.Store
            };

            var depthAttachment = new SDL.GPUDepthStencilTargetInfo
            {
                Texture = _depthTexture,
                LoadOp = GPULoadOp.Clear,
                StoreOp = GPUStoreOp.DontCare,
                ClearDepth = 1.0f,
            };

            SDL.BeginGPURenderPass(cmdbuf, &colorAttachment, 1, &depthAttachment);

            // Run UI logic (for input processing, etc.), but rendering is disabled in SDL3Renderer
            if (_renderer is not null && _textService is not null && _appEngine is not null)
            {
                _appEngine.UpdateAndRender(_renderer, _textService);
            }

            // Draw the 3D cube
            _cubeRenderer?.Draw(cmdbuf, new Vortice.Mathematics.Rect() { X = 0, Y = 0, Width = w, Height = h });

            SDL.EndGPURenderPass((nint)cmdbuf);
        }

        SDL.SubmitGPUCommandBuffer((nint)cmdbuf);
    }

    private void ModalRenderFrame()
    {
        // Modal rendering with SDL_gpu not implemented in this step.
        // For now, it will be a blank window.
        nint cmdbuf = SDL.AcquireGPUCommandBuffer((nint)_gpuDevice);
        
        if (!SDL.AcquireGPUSwapchainTexture(cmdbuf, _windowPtr, out nint bruh, out uint w, out uint h))
        {
            var colorAttachment = new SDL.GPUColorTargetInfo()
            {
                //Texture = swapchainTexture,
                ClearColor = new SDL.FColor { R = (byte)_backgroundColor.R, G = (byte)_backgroundColor.G, B = (byte)_backgroundColor.B, A = (byte)_backgroundColor.A },
                LoadOp = GPULoadOp.Clear,
                StoreOp = GPUStoreOp.Store
            };
            //SDL.BeginGPURenderPass(cmdbuf, colorAttachment, 1, nint.Zero);
            SDL.BeginGPURenderPass(cmdbuf, nint.Zero, 1, nint.Zero);
            SDL.EndGPURenderPass(cmdbuf);
        }

        SDL.SubmitGPUCommandBuffer((nint)cmdbuf);
    }

    private void CreateDepthBuffer()
    {
        SDL.GetWindowSizeInPixels(_windowPtr, out int w, out int h);
        var depthCreateInfo = new SDL.GPUTextureCreateInfo
        {
            Width = (uint)w,
            Height = (uint)h,
            Format = GPUTextureFormat.D16Unorm,
            LayerCountOrDepth = 1,
            NumLevels = 1,
            Usage = GPUTextureUsageFlags.DepthStencilTarget
        };
        _depthTexture = SDL.CreateGPUTexture((nint)_gpuDevice, depthCreateInfo);
    }

    public void Cleanup()
    {
        if (_isDisposed) return;
        Console.WriteLine($"SDL3WindowHost cleanup for '{_title}'...");

        _activeModalWindow?.Cleanup();
        _cubeRenderer?.Cleanup();

        _appEngine?.Cleanup();
        _renderer?.Cleanup();
        _textService?.Cleanup();

        if (_depthTexture != null)
        {
            SDL.DestroyTexture((nint)_depthTexture);
            _depthTexture = null;
        }

        if (_gpuDevice != null)
        {
            SDL.DestroyGPUDevice((nint)_gpuDevice);
            _gpuDevice = null;
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
}