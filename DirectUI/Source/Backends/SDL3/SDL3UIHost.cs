using System.Diagnostics;
using DirectUI.Diagnostics;
using DirectUI.Input;
using SDL3;
using Vortice.Mathematics;

namespace DirectUI.Backends.SDL3;

public unsafe class SDL3UIHost
{
    private readonly Action<UIContext> _drawCallback;
    private readonly Color4 _backgroundColor;
    private readonly nint _rendererPtr;
    private readonly nint _windowPtr;

    private readonly FpsCounter _fpsCounter;
    private readonly InputManager _inputManager;
    private readonly Stopwatch _frameTimer = new();
    private long _lastFrameTicks;

    private SDL3Renderer? _renderer;
    private SDL3TextService? _textService;

    public bool ShowFpsCounter { get; set; } = true;
    public InputManager Input => _inputManager;

    public SDL3UIHost(Action<UIContext> drawCallback, Color4 backgroundColor, nint rendererPtr, nint windowPtr)
    {
        _drawCallback = drawCallback;
        _backgroundColor = backgroundColor;
        _rendererPtr = rendererPtr;
        _windowPtr = windowPtr;

        _fpsCounter = new FpsCounter();
        _inputManager = new InputManager();

        _frameTimer.Start();
        _lastFrameTicks = _frameTimer.ElapsedTicks;
    }

    public bool Initialize()
    {
        _renderer = new SDL3Renderer(_rendererPtr, _windowPtr);
        _textService = new SDL3TextService(); // Minimal for now
        _fpsCounter.Initialize(_textService, _renderer);
        return true;
    }

    public void Cleanup()
    {
        _fpsCounter.Cleanup();
        _renderer?.Cleanup();
        _textService?.Cleanup();
        // The SDL renderer and window are destroyed in ApplicationRunner.
    }

    public void Resize(int width, int height)
    {
        // For SDL, resizing window usually updates renderer automatically,
        // but we might need to tell our renderer implementation the new size.
        _renderer?.UpdateWindowSize(width, height);
    }

    public void Render()
    {
        if (UI.IsRendering) return;
        if (_renderer is null || _textService is null) return;

        // Get mouse position at the start of each frame using window-relative float coordinates
        SDL.GetMouseState(out float mouseX, out float mouseY);
        _inputManager.SetMousePosition((int)mouseX, (int)mouseY);

        long currentTicks = _frameTimer.ElapsedTicks;
        float deltaTime = (float)(currentTicks - _lastFrameTicks) / Stopwatch.Frequency;
        _lastFrameTicks = currentTicks;
        deltaTime = Math.Min(deltaTime, 1.0f / 15.0f);

        _fpsCounter.Update();

        // Clear the renderer buffer
        SDL.SetRenderDrawColor(_rendererPtr, (byte)(_backgroundColor.R * 255), (byte)(_backgroundColor.G * 255), (byte)(_backgroundColor.B * 255), (byte)(_backgroundColor.A * 255));
        SDL.RenderClear(_rendererPtr);

        try
        {
            var inputState = _inputManager.GetCurrentState();
            var uiContext = new UIContext(_renderer, _textService, inputState, deltaTime);
            UI.BeginFrame(uiContext);

            _drawCallback(uiContext);

            if (ShowFpsCounter)
            {
                _fpsCounter.Draw();
            }

            UI.EndFrame();

            _renderer.Flush(); // Flush batched commands, if any (not implemented yet for SDL)
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred during SDL3 drawing: {ex}");
        }
        finally
        {
            SDL.RenderPresent(_rendererPtr); // Present the rendered frame
            _inputManager.PrepareNextFrame();
        }
    }
}