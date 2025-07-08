using System;
using System.Diagnostics;
using DirectUI.Core;
using Vortice.Direct2D1; // Still used for DrawTextOptions enum
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
    private ITextLayout? _fpsTextLayout; // Cached layout

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
        _fpsTextLayout?.Dispose();
        _fpsTextLayout = null;
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

            // Invalidate the cached text layout so it's recreated with the new FPS value
            _fpsTextLayout?.Dispose();
            _fpsTextLayout = null;
        }
    }

    public void Draw()
    {
        if (_textService is null || _renderer is null)
        {
            return;
        }

        string fpsText = $"FPS: {_currentFps:F1}";

        // Lazily create the text layout
        if (_fpsTextLayout == null)
        {
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
            _fpsTextLayout = _textService.GetTextLayout(fpsText, style, new Vector2(float.MaxValue, float.MaxValue), new Alignment(HAlignment.Left, VAlignment.Top));
        }

        if (_fpsTextLayout is null) return;

        _renderer.DrawTextLayout(new Vector2(5f, 5f), _fpsTextLayout, _textColor);
    }
}