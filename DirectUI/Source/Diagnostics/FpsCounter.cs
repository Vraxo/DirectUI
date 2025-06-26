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
    private const long FpsUpdateIntervalTicks = TimeSpan.TicksPerSecond / 2; // Update twice a second

    // State
    private readonly Stopwatch _timer = new();
    private long _lastUpdateTimeTicks = 0;
    private int _frameCountSinceUpdate = 0;
    private float _currentFps = 0.0f;

    // Direct2D Resources
    private ID2D1SolidColorBrush? _textBrush;
    private IDWriteTextFormat? _textFormat;

    public void Initialize(ID2D1RenderTarget renderTarget, IDWriteFactory dwriteFactory)
    {
        ArgumentNullException.ThrowIfNull(renderTarget);
        ArgumentNullException.ThrowIfNull(dwriteFactory);

        Cleanup(); // Ensure any old resources are released

        try
        {
            _textFormat = dwriteFactory.CreateTextFormat(_fontName, null, FontWeight.Normal, FontStyle.Normal, FontStretch.Normal, _fontSize, "en-us");
            _textFormat.TextAlignment = DW.TextAlignment.Leading;
            _textFormat.ParagraphAlignment = ParagraphAlignment.Near;

            _textBrush = renderTarget.CreateSolidColorBrush(_textColor);

            Console.WriteLine("Created FPS drawing resources.");

            _frameCountSinceUpdate = 0;
            _currentFps = 0;
            _lastUpdateTimeTicks = 0;
            _timer.Restart();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to create FPS counter drawing resources: {ex.Message}");
            Cleanup(); // Clean up partial resources on failure
        }
    }

    public void Cleanup()
    {
        _timer.Stop();
        _textBrush?.Dispose();
        _textBrush = null;
        _textFormat?.Dispose();
        _textFormat = null;
    }

    public void HandleResize(ID2D1RenderTarget renderTarget)
    {
        // The text format is device-independent, but the brush is not.
        _textBrush?.Dispose();
        _textBrush = null;
        try
        {
            if (renderTarget is not null)
            {
                _textBrush = renderTarget.CreateSolidColorBrush(_textColor);
                Console.WriteLine("Recreated FPS brush after resize.");
            }
        }
        catch (Exception brushEx)
        {
            Console.WriteLine($"Warning: Failed to recreate FPS brush after resize: {brushEx.Message}");
        }
    }

    public bool Update()
    {
        if (!_timer.IsRunning)
        {
            _timer.Start();
            _lastUpdateTimeTicks = _timer.ElapsedTicks;
            _frameCountSinceUpdate = 0;
        }

        _frameCountSinceUpdate++;
        long elapsedTicks = _timer.ElapsedTicks;
        long timeSinceLastUpdate = elapsedTicks - _lastUpdateTimeTicks;

        if (timeSinceLastUpdate >= FpsUpdateIntervalTicks)
        {
            float secondsElapsed = (float)timeSinceLastUpdate / TimeSpan.TicksPerSecond;
            _currentFps = (secondsElapsed > 0.001f) ? (_frameCountSinceUpdate / secondsElapsed) : 0.0f;
            _frameCountSinceUpdate = 0;
            _lastUpdateTimeTicks = elapsedTicks;
            return true; // Indicates the text has changed, so a redraw is needed.
        }

        return false;
    }

    public void Draw(ID2D1RenderTarget renderTarget)
    {
        if (_textBrush is null || _textFormat is null)
        {
            return;
        }

        string fpsText = $"FPS: {_currentFps:F1}";
        var layoutRect = new Rect(5f, 5f, 150f, 30f);
        renderTarget.DrawText(fpsText, _textFormat, layoutRect, _textBrush);
    }
}