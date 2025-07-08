// AppHost.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using DirectUI.Backends; // Added Raylib namespace
using DirectUI.Core; // For IRenderer, ITextService
using DirectUI.Diagnostics;
using DirectUI.Input;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;
using SizeI = Vortice.Mathematics.SizeI;
using Raylib_cs; // Added for Raylib backend

namespace DirectUI;

/// <summary>
/// Manages the application's rendering lifecycle, graphics device, and input state aggregation.
/// This class acts as the "engine" that is hosted by a window.
/// </summary>
public class AppHost
{
    private readonly Action<UIContext> _drawCallback;
    private readonly Color4 _backgroundColor;
    private readonly FpsCounter _fpsCounter;
    private readonly InputManager _inputManager;
    private readonly Stopwatch _frameTimer = new();
    private long _lastFrameTicks;

    private GraphicsDevice? _graphicsDevice; // Only used for D2D backend
    private IntPtr _hwnd; // Only used for D2D backend

    private IRenderer? _renderer;
    private ITextService? _textService;
    private readonly bool _useRaylibBackend; // Flag to choose backend

    public bool ShowFpsCounter { get; set; } = true;
    public InputManager Input => _inputManager;

    public AppHost(Action<UIContext> drawCallback, Color4 backgroundColor, bool useRaylibBackend = false)
    {
        _drawCallback = drawCallback ?? throw new ArgumentNullException(nameof(drawCallback));
        _backgroundColor = backgroundColor;
        _fpsCounter = new FpsCounter();
        _inputManager = new InputManager();
        _useRaylibBackend = useRaylibBackend;

        _frameTimer.Start();
        _lastFrameTicks = _frameTimer.ElapsedTicks;
    }

    public bool Initialize(IntPtr hwnd, SizeI clientSize)
    {
        _hwnd = hwnd; // Only relevant for D2D backend

        if (_useRaylibBackend)
        {
            // Raylib initialization is usually done once globally (e.g. in Program.cs).
            // This AppHost simply creates the renderer/text service.
            _renderer = new RaylibRenderer();
            _textService = new RaylibTextService();
        }
        else // Direct2D Backend
        {
            if (_graphicsDevice?.IsInitialized ?? false) return true;
            if (_hwnd == IntPtr.Zero) return false;

            _graphicsDevice ??= new GraphicsDevice();

            if (!_graphicsDevice.Initialize(_hwnd, clientSize))
            {
                return false;
            }

            // Initialize backend services using the concrete D2D and DWrite factories
            if (_graphicsDevice.RenderTarget != null && _graphicsDevice.DWriteFactory != null)
            {
                _renderer = new Direct2DRenderer(_graphicsDevice.RenderTarget, _graphicsDevice.DWriteFactory); // Pass DWriteFactory
                _textService = new DirectWriteTextService(_graphicsDevice.DWriteFactory);
            }
            else
            {
                Console.WriteLine("CRITICAL: GraphicsDevice did not provide valid RenderTarget or DWriteFactory for D2D backend initialization.");
                return false;
            }
        }

        // Initialize the FpsCounter using the selected backend services
        if (_textService != null && _renderer != null)
        {
            _fpsCounter.Initialize(_textService, _renderer);
        }
        else
        {
            Console.WriteLine("CRITICAL: Renderer or TextService was not available for FpsCounter initialization.");
            return false;
        }
        return true;
    }

    public void Cleanup()
    {
        _fpsCounter.Cleanup();
        _textService?.Cleanup();
        (_renderer as Direct2DRenderer)?.Cleanup(); // Specific cleanup for Direct2DRenderer
        (_renderer as RaylibRenderer)?.Cleanup(); // Specific cleanup for RaylibRenderer (if any needed in future)
        _graphicsDevice?.Cleanup();
    }

    public void Resize(SizeI newSize)
    {
        if (_useRaylibBackend)
        {
            // Raylib window resizing is typically handled externally,
            // but the renderer's RenderTargetSize will adapt.
        }
        else // Direct2D Backend
        {
            if (_graphicsDevice?.IsInitialized ?? false)
            {
                _graphicsDevice.Resize(newSize);
            }
            else if (_hwnd != IntPtr.Zero)
            {
                Initialize(_hwnd, GetClientRectSizeForHost());
            }
        }
    }

    public void Render()
    {
        // Prevent re-entrant rendering calls, which can happen if a new window
        // is created and painted synchronously inside another window's render loop.
        if (UI.IsRendering) return;

        // Ensure backend services are initialized
        if (_renderer is null || _textService is null)
        {
            if (_useRaylibBackend)
            {
                // For Raylib, AppHost doesn't create the window, so we can't get its size here.
                // Assuming Raylib.IsWindowReady() is true and window is sized externally.
                Initialize(IntPtr.Zero, new SizeI(Raylib.GetScreenWidth(), Raylib.GetScreenHeight()));
            }
            else
            {
                if (!Initialize(_hwnd, GetClientRectSizeForHost()))
                {
                    _inputManager.PrepareNextFrame();
                    return;
                }
            }
        }

        // Calculate delta time for the frame
        long currentTicks = _frameTimer.ElapsedTicks;
        float deltaTime = (float)(currentTicks - _lastFrameTicks) / Stopwatch.Frequency;
        _lastFrameTicks = currentTicks;

        // Clamp delta time to avoid huge jumps (e.g., when debugging or window is moved)
        deltaTime = Math.Min(deltaTime, 1.0f / 15.0f); // Clamp to a minimum of 15 FPS

        _fpsCounter.Update(); // Update FPS counter once per render call.

        // Begin drawing for the chosen backend
        if (_useRaylibBackend)
        {
            Raylib.BeginDrawing();
            Raylib.ClearBackground(new Raylib_cs.Color(
                (byte)(_backgroundColor.R * 255),
                (byte)(_backgroundColor.G * 255),
                (byte)(_backgroundColor.B * 255),
                (byte)(_backgroundColor.A * 255)
            ));
        }
        else // Direct2D Backend
        {
            _graphicsDevice!.BeginDraw();
        }

        try
        {
            // Get the immutable input state for this frame from the InputManager
            var inputState = _inputManager.GetCurrentState();

            var uiContext = new UIContext(_renderer!, _textService!, inputState, deltaTime);
            UI.BeginFrame(uiContext);

            _drawCallback(uiContext);

            if (ShowFpsCounter)
            {
                _fpsCounter.Draw();
            }

            UI.EndFrame();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred during drawing: {ex}");
            if (!_useRaylibBackend)
            {
                _graphicsDevice?.Cleanup();
            }
        }
        finally
        {
            // End drawing for the chosen backend
            if (_useRaylibBackend)
            {
                Raylib.EndDrawing();
            }
            else // Direct2D Backend
            {
                _graphicsDevice?.EndDraw();
            }
            _inputManager.PrepareNextFrame();
        }
    }

    private SizeI GetClientRectSizeForHost()
    {
        if (_hwnd != IntPtr.Zero && NativeMethods.GetClientRect(_hwnd, out NativeMethods.RECT r))
        {
            int width = Math.Max(1, r.right - r.left);
            int height = Math.Max(1, r.bottom - r.top);
            return new SizeI(width, height);
        }
        // Fallback for when the handle is not yet valid during initialization.
        // This size isn't critical as resize will be called immediately after.
        return new SizeI(1, 1);
    }
}