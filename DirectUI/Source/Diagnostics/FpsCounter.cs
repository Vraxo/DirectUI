using System;
using System.Diagnostics;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;
using DW = Vortice.DirectWrite;

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

    // Direct2D Resources (Device-Independent only)
    private IDWriteTextFormat? _textFormat;

    public void Initialize(IDWriteFactory dwriteFactory)
    {
        ArgumentNullException.ThrowIfNull(dwriteFactory);

        Cleanup(); // Ensure any old resources are released

        try
        {
            _textFormat = dwriteFactory.CreateTextFormat(_fontName, null, FontWeight.Normal, FontStyle.Normal, FontStretch.Normal, _fontSize, "en-us");
            _textFormat.TextAlignment = DW.TextAlignment.Leading;
            _textFormat.ParagraphAlignment = ParagraphAlignment.Near;

            Console.WriteLine("Created FPS text format resource.");

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
            Console.WriteLine($"Warning: Failed to create FPS counter text format: {ex.Message}");
            Cleanup(); // Clean up partial resources on failure
        }
    }

    public void Cleanup()
    {
        _timer.Stop();
        _textFormat?.Dispose();
        _textFormat = null;
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
        }
    }

    public void Draw(ID2D1RenderTarget renderTarget)
    {
        if (_textFormat is null || UI.Resources is null)
        {
            return;
        }

        var textBrush = UI.Resources.GetOrCreateBrush(renderTarget, _textColor);
        if (textBrush is null)
        {
            return;
        }

        string fpsText = $"FPS: {_currentFps:F1}";
        var layoutRect = new Rect(5f, 5f, 150f, 30f);
        renderTarget.DrawText(fpsText, _textFormat, layoutRect, textBrush);
    }
}