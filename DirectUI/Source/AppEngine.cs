// AppEngine.cs
using System;
using System.Diagnostics;
using System.Numerics;
using Vortice.Mathematics;
using DirectUI.Core; // For IRenderer, ITextService
using DirectUI.Drawing;
using DirectUI.Diagnostics;
using DirectUI.Input;

namespace DirectUI;

/// <summary>
/// Manages the application's core UI engine lifecycle, input state aggregation,
/// and frame timing. It is decoupled from any specific windowing or graphics backend.
/// </summary>
public class AppEngine
{
    private readonly Action<UIContext> _drawCallback;
    private readonly Color4 _backgroundColor;
    private readonly FpsCounter _fpsCounter;
    private readonly InputManager _inputManager;
    private readonly Stopwatch _frameTimer = new();
    private long _lastFrameTicks;

    public bool ShowFpsCounter { get; set; } = true;
    public InputManager Input => _inputManager;

    public AppEngine(Action<UIContext> drawCallback, Color4 backgroundColor)
    {
        _drawCallback = drawCallback ?? throw new ArgumentNullException(nameof(drawCallback));
        _backgroundColor = backgroundColor;
        _fpsCounter = new FpsCounter();
        _inputManager = new InputManager();

        _frameTimer.Start();
        _lastFrameTicks = _frameTimer.ElapsedTicks;
    }

    /// <summary>
    /// Initializes internal components like the FPS counter.
    /// This should be called once after the renderer and text service are ready.
    /// </summary>
    public void Initialize(ITextService textService, IRenderer renderer)
    {
        _fpsCounter.Initialize(textService, renderer);
    }

    /// <summary>
    /// Cleans up internal engine resources.
    /// </summary>
    public void Cleanup()
    {
        _fpsCounter.Cleanup();
    }

    /// <summary>
    /// Updates the engine state for a single frame and renders the UI.
    /// This method is called by the specific window host after it has prepared its drawing surface.
    /// </summary>
    /// <param name="renderer">The graphics renderer for this frame.</param>
    /// <param name="textService">The text service for this frame.</param>
    public void UpdateAndRender(IRenderer renderer, ITextService textService)
    {
        // Prevent re-entrant rendering calls.
        if (UI.IsRendering) return;

        // Calculate delta time for the frame
        long currentTicks = _frameTimer.ElapsedTicks;
        float deltaTime = (float)(currentTicks - _lastFrameTicks) / Stopwatch.Frequency;
        _lastFrameTicks = currentTicks;

        // Clamp delta time to avoid huge jumps (e.g., when debugging or window is moved)
        deltaTime = Math.Min(deltaTime, 1.0f / 15.0f); // Clamp to a minimum of 15 FPS

        _fpsCounter.Update(); // Update FPS counter once per render call.

        try
        {
            // Get the immutable input state for this frame from the InputManager
            var inputState = _inputManager.GetCurrentState();

            var uiContext = new UIContext(renderer, textService, inputState, deltaTime);
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
            Console.WriteLine($"An error occurred during UI drawing: {ex}");
        }
        finally
        {
            _inputManager.PrepareNextFrame();
        }
    }
}
