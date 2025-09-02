using System.Numerics;
using DirectUI;
using Sonorize.Audio;

namespace Sonorize;

public class PlaybackControlsView
{
    private readonly PlaybackManager _playbackManager;

    public PlaybackControlsView(PlaybackManager playbackManager)
    {
        _playbackManager = playbackManager;
    }

    public void Draw(UIContext context)
    {
        float playbackControlsHeight = 70f;
        float padding = 10f;
        var windowSize = context.Renderer.RenderTargetSize;
        var controlsY = windowSize.Y - playbackControlsHeight;
        var controlsRect = new Vortice.Mathematics.Rect(0, controlsY, windowSize.X, playbackControlsHeight);

        DrawBackground(context, controlsRect);

        UI.BeginVBoxContainer("playbackVBox", new Vector2(padding, controlsY + 5), 5);
        {
            float sliderWidth = windowSize.X - (padding * 2);
            DrawSeekSlider(context, sliderWidth);
            DrawControlButtons(context);
        }
        UI.EndVBoxContainer();
    }

    private static void DrawBackground(UIContext context, Vortice.Mathematics.Rect controlsRect)
    {
        var bgStyle = new BoxStyle
        {
            FillColor = new(0.1f, 0.1f, 0.1f, 1.0f),
            BorderColor = new(0.05f, 0.05f, 0.05f, 1.0f),
            BorderLengthTop = 1f,
            BorderLengthBottom = 0,
            BorderLengthLeft = 0,
            BorderLengthRight = 0,
            Roundness = 0
        };
        context.Renderer.DrawBox(controlsRect, bgStyle);
    }

    private void DrawSeekSlider(UIContext context, float sliderWidth)
    {
        double currentPosition = _playbackManager.CurrentPosition;
        double totalDuration = _playbackManager.TotalDuration;
        bool isAudioLoaded = totalDuration > 0;

        var sliderSize = new Vector2(sliderWidth, 10);
        float sliderValue = (float)currentPosition;

        float newSliderValue = UI.HSlider(
            id: "seekSlider",
            currentValue: sliderValue,
            minValue: 0f,
            maxValue: isAudioLoaded ? (float)totalDuration : 1.0f,
            size: sliderSize,
            disabled: !isAudioLoaded,
            grabberSize: new Vector2(10, 20)
        );

        if (isAudioLoaded && Math.Abs(newSliderValue - sliderValue) > 0.01f)
        {
            _playbackManager.Seek(newSliderValue);
        }
    }

    private void DrawControlButtons(UIContext context)
    {
        bool isAnyTrackAvailable = _playbackManager.HasTracks;
        var windowSize = context.Renderer.RenderTargetSize;

        var buttonSize = new Vector2(35, 24);
        var totalButtonsWidth = (buttonSize.X * 5) + (5 * 4); // 5 buttons, 4 gaps
        var buttonsStartX = (windowSize.X - totalButtonsWidth) / 2;
        var buttonsY = UI.Context.Layout.GetCurrentPosition().Y + 5;
        const int controlsLayer = 10;

        var iconButtonTheme = new ButtonStylePack { FontSize = 16f };
        var toggleButtonTheme = new ButtonStylePack { FontSize = 16f };
        toggleButtonTheme.Active.FillColor = DefaultTheme.Accent;
        toggleButtonTheme.Active.BorderColor = DefaultTheme.AccentBorder;
        toggleButtonTheme.ActiveHover.FillColor = DefaultTheme.Accent;
        toggleButtonTheme.ActiveHover.BorderColor = DefaultTheme.AccentBorder;

        UI.BeginHBoxContainer("playbackButtons", new Vector2(buttonsStartX, buttonsY), 5);
        {
            bool isShuffleActive = _playbackManager.Mode == PlaybackMode.Shuffle;
            if (UI.Button("shuffle", "🔀", buttonSize, theme: toggleButtonTheme, disabled: !isAnyTrackAvailable, layer: controlsLayer, isActive: isShuffleActive))
            {
                _playbackManager.Mode = isShuffleActive ? PlaybackMode.Sequential : PlaybackMode.Shuffle;
            }

            if (UI.Button("prevTrack", "⏮", buttonSize, theme: iconButtonTheme, disabled: !isAnyTrackAvailable, layer: controlsLayer))
            {
                _playbackManager.Previous();
            }

            string playPauseText = _playbackManager.IsPlaying ? "⏸" : "▶";
            if (UI.Button("playPause", playPauseText, buttonSize, theme: iconButtonTheme, disabled: !isAnyTrackAvailable, layer: controlsLayer))
            {
                _playbackManager.TogglePlayPause();
            }

            if (UI.Button("nextTrack", "⏭", buttonSize, theme: iconButtonTheme, disabled: !isAnyTrackAvailable, layer: controlsLayer))
            {
                _playbackManager.Next();
            }

            string repeatText = _playbackManager.EndAction switch
            {
                PlaybackEndAction.NextInQueue => "🔁",
                PlaybackEndAction.RepeatSong => "🔂",
                PlaybackEndAction.DoNothing => "➡",
                _ => "?"
            };
            if (UI.Button("repeatMode", repeatText, buttonSize, theme: iconButtonTheme, disabled: !isAnyTrackAvailable, layer: controlsLayer))
            {
                _playbackManager.EndAction = (PlaybackEndAction)(((int)_playbackManager.EndAction + 1) % 3);
            }
        }
        UI.EndHBoxContainer();
    }
}