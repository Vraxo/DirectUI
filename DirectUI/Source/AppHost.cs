// AppHost.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using DirectUI.Backends;
using DirectUI.Core;
using DirectUI.Diagnostics;
using DirectUI.Input;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;
using SizeI = Vortice.Mathematics.SizeI;

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

    private GraphicsDevice? _graphicsDevice;
    private IntPtr _hwnd;

    private IRenderer? _renderer;
    private ITextService? _textService;

    public bool ShowFpsCounter { get; set; } = true;
    public InputManager Input => _inputManager;

    public AppHost(Action<UIContext> drawCallback, Color4 backgroundColor)
    {
        _drawCallback = drawCallback ?? throw new ArgumentNullException(nameof(drawCallback));
        _backgroundColor = backgroundColor;
        _fpsCounter = new FpsCounter();
        _inputManager = new InputManager();

        _frameTimer.Start();
        _lastFrameTicks = _frameTimer.ElapsedTicks;
    }

    public bool Initialize(IntPtr hwnd, SizeI clientSize)
    {
        _hwnd = hwnd;
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
            _renderer = new Direct2DRenderer(_graphicsDevice.RenderTarget);
            _textService = new DirectWriteTextService(_graphicsDevice.DWriteFactory);

            // Initialize the FpsCounter once during construction.
            _fpsCounter.Initialize(_textService, _renderer);
        }
        else
        {
            Console.WriteLine("CRITICAL: GraphicsDevice did not provide valid RenderTarget or DWriteFactory for backend initialization.");
            return false;
        }

        return true;
    }

    public void Cleanup()
    {
        _fpsCounter.Cleanup();
        _textService?.Cleanup();
        (_renderer as Direct2DRenderer)?.Cleanup(); // Specific cleanup for Direct2DRenderer
        _graphicsDevice?.Cleanup();
    }

    public void Resize(SizeI newSize)
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

    public void Render()
    {
        // Prevent re-entrant rendering calls, which can happen if a new window
        // is created and painted synchronously inside another window's render loop.
        if (UI.IsRendering) return;

        if (!(_graphicsDevice?.IsInitialized ?? false) || _renderer is null || _textService is null)
        {
            if (!Initialize(_hwnd, GetClientRectSizeForHost()))
            {
                // On failure, still need to prepare the input manager for the next attempt.
                _inputManager.PrepareNextFrame();
                return;
            }
        }

        // Calculate delta time for the frame
        long currentTicks = _frameTimer.ElapsedTicks;
        float deltaTime = (float)(currentTicks - _lastFrameTicks) / Stopwatch.Frequency;
        _lastFrameTicks = currentTicks;

        // Clamp delta time to avoid huge jumps (e.g., when debugging or window is moved)
        deltaTime = Math.Min(deltaTime, 1.0f / 15.0f); // Clamp to a minimum of 15 FPS

        _fpsCounter.Update(); // Update FPS counter once per render call.

        _graphicsDevice!.BeginDraw();

        try
        {
            _graphicsDevice.RenderTarget!.Clear(_backgroundColor); // Clear the actual D2D render target

            // Get the immutable input state for this frame from the InputManager
            var inputState = _inputManager.GetCurrentState();

            var uiContext = new UIContext(_renderer!, _textService!, inputState, deltaTime);
            UI.BeginFrame(uiContext);

            _drawCallback(uiContext);

            if (ShowFpsCounter)
            {
                _fpsCounter.Draw(); // FpsCounter now uses its own internal renderer ref
            }

            UI.EndFrame();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred during drawing: {ex}");
            _graphicsDevice.Cleanup();
        }
        finally
        {
            _graphicsDevice.EndDraw();
            // Prepare the input manager for the next frame.
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