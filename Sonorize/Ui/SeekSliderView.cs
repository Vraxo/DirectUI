using DirectUI;
using DirectUI.Drawing;
using Sonorize.Audio;
using System.Numerics;

namespace Sonorize;

public class SeekSliderView
{
    private readonly PlaybackManager _playbackManager;
    private bool _isSeekSliderDragging;
    private float _seekSliderValueDuringDrag;

    public SeekSliderView(PlaybackManager playbackManager)
    {
        _playbackManager = playbackManager;
    }

    public void Draw(UIContext context, float panelWidth)
    {
        double currentPos = _playbackManager.CurrentPosition;
        double totalDuration = _playbackManager.TotalDuration;
        bool isLoaded = totalDuration > 0;

        string currentTimeStr = FormatTime(currentPos);
        string totalTimeStr = FormatTime(totalDuration);
        var timeStyle = new ButtonStyle { FontSize = 12, FontColor = new(0.7f, 0.7f, 0.7f, 1f) };
        float timeLabelWidth = context.TextService.MeasureText("00:00", timeStyle).X + 10;

        float sliderWidth = panelWidth - (timeLabelWidth * 2);

        UI.BeginHBoxContainer("seekHBox", Vector2.Zero, 0, VAlignment.Center, 20);
        {
            UI.Text("currentTime", currentTimeStr, new(timeLabelWidth, 20), timeStyle, new(HAlignment.Right, VAlignment.Center));

            DrawSeekSlider(sliderWidth, isLoaded, (float)currentPos, (float)totalDuration);

            UI.Text("totalTime", totalTimeStr, new(timeLabelWidth, 20), timeStyle, new(HAlignment.Left, VAlignment.Center));
        }
        UI.EndHBoxContainer();
    }

    private void DrawSeekSlider(float sliderWidth, bool isLoaded, float currentPos, float totalDuration)
    {
        SliderStyle sliderTheme = new SliderStyle();
        sliderTheme.Background.FillColor = new(0.3f, 0.3f, 0.3f, 1f);
        sliderTheme.Foreground.FillColor = new(0.9f, 0.9f, 0.9f, 1f);
        sliderTheme.ForegroundHover.FillColor = new(30, 215, 96, 255);

        ButtonStylePack grabberTheme = new() { Roundness = 1f };
        grabberTheme.Normal.FillColor = Colors.Transparent;
        grabberTheme.Normal.BorderColor = Colors.Transparent;
        grabberTheme.Hover.FillColor = Colors.White;
        grabberTheme.Hover.BorderColor = Colors.Transparent;
        grabberTheme.Pressed.FillColor = Colors.White;
        grabberTheme.Pressed.BorderColor = Colors.Transparent;

        int sliderId = "seekSlider".GetHashCode();
        bool isDragging = UI.State.ActivelyPressedElementId == sliderId;
        float sliderValue = _isSeekSliderDragging ? _seekSliderValueDuringDrag : currentPos;

        float newValue = UI.HSlider("seekSlider", sliderValue, 0, isLoaded ? totalDuration : 1f, new(sliderWidth, 8),
            theme: sliderTheme,
            grabberTheme: grabberTheme,
            disabled: !isLoaded,
            grabberSize: new(14, 14));

        if (isDragging)
        {
            _seekSliderValueDuringDrag = newValue;
        }

        if (_isSeekSliderDragging && !isDragging && isLoaded)
        {
            _playbackManager.Seek(_seekSliderValueDuringDrag);
        }

        _isSeekSliderDragging = isDragging;
    }

    private static string FormatTime(double seconds)
    {
        return (double.IsNaN(seconds) || seconds < 0) 
            ? "00:00" 
            : TimeSpan.FromSeconds(seconds).ToString(@"mm\:ss");
    }
}