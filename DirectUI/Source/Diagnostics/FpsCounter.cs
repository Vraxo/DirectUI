// DirectUI/Diagnostics/FpsCounter.cs
using System;
using System.Diagnostics;
using DirectUI.Core;
using Vortice.Mathematics;
using Vortice.DirectWrite;
using System.Numerics; // Still used for TextAlignment and ParagraphAlignment enums

namespace DirectUI.Diagnostics;

public class FpsCounter
{
    // Configuration
    private readonly Color4 _textColor = DefaultTheme.Text;
    private readonly string _fontName = "Consolas";
    private readonly float _fontSize = 14.0f;
    private const int UpdatesPerSecond = 2; // Update twice a second

    // State
    private readonly Stopwatch _timer = new();
    private long _lastUpdateTimeTicks = 0;
    private int _frameCountSinceUpdate = 0;
    private float _currentFps = 0.0f;
    private long _updateIntervalInStopwatchTicks;

    // DirectUI Service references
    private ITextService? _textService;
    private IRenderer? _renderer;
    // Removed ITextLayout? _fpsTextLayout; // No longer cached here for drawing

    public void Initialize(ITextService textService, IRenderer renderer)
    {
        _textService = textService ?? throw new ArgumentNullException(nameof(textService));
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));

        Cleanup(); // Ensure any old resources are released

        try
        {
            // Calculate the update interval based on the Stopwatch's actual frequency.
            if (Stopwatch.IsHighResolution)
            {
                _updateIntervalInStopwatchTicks = Stopwatch.Frequency / UpdatesPerSecond;
            }
            else
            {
                // Fallback for low-resolution timer
                _updateIntervalInStopwatchTicks = TimeSpan.TicksPerSecond / UpdatesPerSecond;
            }

            _frameCountSinceUpdate = 0;
            _currentFps = 0;
            _lastUpdateTimeTicks = 0;
            _timer.Restart();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to initialize FPS counter: {ex.Message}");
            Cleanup(); // Clean up partial resources on failure
        }
    }

    public void Cleanup()
    {
        _timer.Stop();
        // Removed _fpsTextLayout?.Dispose();
        // Removed _fpsTextLayout = null;
    }

    /// <summary>
    /// Updates the frame count and recalculates the FPS value if the update interval has passed.
    /// This should be called once per rendered frame.
    /// </summary>
    public void Update()
    {
        if (!_timer.IsRunning) _timer.Start();

        _frameCountSinceUpdate++;
        long elapsedTicks = _timer.ElapsedTicks;
        long timeSinceLastUpdate = elapsedTicks - _lastUpdateTimeTicks;

        if (timeSinceLastUpdate >= _updateIntervalInStopwatchTicks)
        {
            long frequency = Stopwatch.IsHighResolution ? Stopwatch.Frequency : TimeSpan.TicksPerSecond;
            float secondsElapsed = (float)timeSinceLastUpdate / frequency;

            _currentFps = (secondsElapsed > 0.001f) ? (_frameCountSinceUpdate / secondsElapsed) : 0.0f;
            _frameCountSinceUpdate = 0;
            _lastUpdateTimeTicks = elapsedTicks;

            // No longer invalidate the cached text layout here, as renderer handles its own caching.
        }
    }

    public void Draw()
    {
        if (_textService is null || _renderer is null)
        {
            return;
        }

        string fpsText = $"FPS: {_currentFps:F1}";

        // The FpsCounter no longer needs to create/cache an ITextLayout for drawing.
        // It simply provides the text and style, and the renderer draws it.
        var style = new ButtonStyle
        {
            FontName = _fontName,
            FontSize = _fontSize,
            FontWeight = FontWeight.Normal,
            FontStyle = FontStyle.Normal,
            FontStretch = FontStretch.Normal,
            FontColor = _textColor
        };
        // Use a large max size as FPS counter typically won't wrap
        // The IRenderer.DrawText method requires a maxSize and alignment for its internal layout logic.
        _renderer.DrawText(new Vector2(5f, 5f), fpsText, style, new Alignment(HAlignment.Left, VAlignment.Top), new Vector2(150f, 30f), _textColor);
    }
}