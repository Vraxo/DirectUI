using DirectUI.Backends.Vulkan;
using DirectUI.Diagnostics;
using DirectUI.Input;
using System.Diagnostics;
using System;
using Veldrid;
using Vortice.Mathematics;

using System;
using System.Diagnostics;
using System.Numerics;
using DirectUI.Core;
using DirectUI.Diagnostics;
using DirectUI.Input;
using Veldrid;
using Vortice.Mathematics;

namespace DirectUI.Backends.Vulkan;

/// <summary>
/// Manages the application's rendering lifecycle for the Veldrid backend.
/// This is the Veldrid equivalent of AppHost.
/// </summary>
public class VeldridUIHost
{
    private readonly Action<UIContext> _drawCallback;
    private readonly Color4 _backgroundColor;
    private readonly Veldrid.GraphicsDevice _gd;
    private readonly CommandList _cl;

    private readonly FpsCounter _fpsCounter;
    private readonly InputManager _inputManager;
    private readonly Stopwatch _frameTimer = new();
    private long _lastFrameTicks;

    private VeldridRenderer? _renderer;
    private VeldridTextService? _textService;

    public bool ShowFpsCounter { get; set; } = true;
    public InputManager Input => _inputManager;

    public VeldridUIHost(Action<UIContext> drawCallback, Color4 backgroundColor, Veldrid.GraphicsDevice gd)
    {
        _drawCallback = drawCallback;
        _backgroundColor = backgroundColor;
        _gd = gd;
        _cl = gd.ResourceFactory.CreateCommandList();

        _fpsCounter = new FpsCounter();
        _inputManager = new InputManager();

        _frameTimer.Start();
        _lastFrameTicks = _frameTimer.ElapsedTicks;
    }

    public bool Initialize()
    {
        _renderer = new VeldridRenderer(_gd, _cl);
        _textService = new VeldridTextService(_gd);
        _fpsCounter.Initialize(_textService, _renderer);
        return true;
    }

    public void Cleanup()
    {
        _fpsCounter.Cleanup();
        _renderer?.Cleanup();
        _textService?.Cleanup();
        _cl.Dispose();
    }

    public void Resize(uint width, uint height)
    {
        _gd.ResizeMainWindow(width, height);
    }

    public void Render()
    {
        if (UI.IsRendering) return;
        if (_renderer is null || _textService is null) return;

        long currentTicks = _frameTimer.ElapsedTicks;
        float deltaTime = (float)(currentTicks - _lastFrameTicks) / Stopwatch.Frequency;
        _lastFrameTicks = currentTicks;
        deltaTime = Math.Min(deltaTime, 1.0f / 15.0f);

        _fpsCounter.Update();

        _cl.Begin();
        _cl.SetFramebuffer(_gd.MainSwapchain.Framebuffer);

        var clearColor = new RgbaFloat(_backgroundColor.R, _backgroundColor.G, _backgroundColor.B, _backgroundColor.A);
        _cl.ClearColorTarget(0, clearColor);

        try
        {
            _renderer.BeginFrame();

            var inputState = _inputManager.GetCurrentState();
            var uiContext = new UIContext(_renderer, _textService, inputState, deltaTime);
            UI.BeginFrame(uiContext);

            _drawCallback(uiContext);

            if (ShowFpsCounter)
            {
                _fpsCounter.Draw();
            }

            UI.EndFrame();

            // This is the critical fix. It tells the renderer to flush any batched text commands.
            _renderer.Flush();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred during Veldrid drawing: {ex}");
        }
        finally
        {
            _cl.End();
            _gd.SubmitCommands(_cl);
            _gd.SwapBuffers();
            _inputManager.PrepareNextFrame();
        }
    }
}