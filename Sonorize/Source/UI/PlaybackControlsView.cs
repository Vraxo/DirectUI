using System.Numerics;
using DirectUI;
using Sonorize.Audio;

namespace Sonorize;

public class PlaybackControlsView
{
    private readonly PlaybackManager _playbackManager;
    private bool _isSeekSliderDragging;
    private float _seekSliderValueDuringDrag;

    public PlaybackControlsView(PlaybackManager playbackManager)
    {
        _playbackManager = playbackManager;
    }

    public void Draw(UIContext context)
    {
        float playbackControlsHeight = 70f;
        float padding = 10f;
        Vector2 windowSize = context.Renderer.RenderTargetSize;
        float controlsY = windowSize.Y - playbackControlsHeight;
        Vortice.Mathematics.Rect controlsRect = new(0, controlsY, windowSize.X, playbackControlsHeight);

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
        context.Renderer.DrawBox(controlsRect, new()
        {
            FillColor = new(0.1f, 0.1f, 0.1f, 1.0f),
            BorderColor = new(0.05f, 0.05f, 0.05f, 1.0f),
            BorderLengthTop = 1f,
            BorderLengthBottom = 0,
            BorderLengthLeft = 0,
            BorderLengthRight = 0,
            Roundness = 0
        });
    }

    private void DrawSeekSlider(UIContext context, float sliderWidth)
    {
        double currentPosition = _playbackManager.CurrentPosition;
        double totalDuration = _playbackManager.TotalDuration;
        bool isAudioLoaded = totalDuration > 0;

        int seekSliderId = "seekSlider".GetHashCode();
        bool isCurrentlyDragging = UI.State.ActivelyPressedElementId == seekSliderId;

        // The value passed TO the slider. If not dragging, it's the player's real time.
        // If we are dragging, it's the value from the last frame's drag to provide continuity.
        float sliderInputValue = _isSeekSliderDragging ? _seekSliderValueDuringDrag : (float)currentPosition;

        Vector2 sliderSize = new(sliderWidth, 10);
        ButtonStylePack grabberTheme = new() { Roundness = 1.0f };
        SliderStyle theme = new() { Background = { Roundness = 1 } };

        float newSliderValue = UI.HSlider(
            id: "seekSlider",
            currentValue: sliderInputValue,
            minValue: 0f,
            maxValue: isAudioLoaded ? (float)totalDuration : 1.0f,
            size: sliderSize,
            disabled: !isAudioLoaded,
            grabberSize: new(16, 16),
            grabberTheme: grabberTheme,
            theme: theme
        );

        // If dragging, store the new value. The audio is NOT updated yet.
        if (isCurrentlyDragging)
        {
            _seekSliderValueDuringDrag = newSliderValue;
        }

        // If we just released the slider, NOW we seek the audio.
        if (_isSeekSliderDragging && !isCurrentlyDragging)
        {
            if (isAudioLoaded)
            {
                // Use the value from the final frame of dragging.
                _playbackManager.Seek(_seekSliderValueDuringDrag);
            }
        }

        // Update state for next frame.
        _isSeekSliderDragging = isCurrentlyDragging;
    }

    private void DrawControlButtons(UIContext context)
    {
        bool isAnyTrackAvailable = _playbackManager.HasTracks;
        var windowSize = context.Renderer.RenderTargetSize;

        Vector2 buttonSize = new(35, 24);
        float totalButtonsWidth = (buttonSize.X * 5) + (5 * 4); // 5 buttons, 4 gaps
        float buttonsStartX = (windowSize.X - totalButtonsWidth) / 2;
        float buttonsY = UI.Context.Layout.GetCurrentPosition().Y + 5;
        const int controlsLayer = 10;

        ButtonStylePack iconButtonTheme = new()
        {
            FontSize = 16f,
            FontName = "Segoe UI Emoji"
        };

        ButtonStylePack toggleButtonTheme = new()
        {
            FontSize = 16f,
            FontName = "Segoe UI Emoji"
        };

        toggleButtonTheme.Active.FillColor = DefaultTheme.Accent;
        toggleButtonTheme.Active.BorderColor = DefaultTheme.AccentBorder;
        toggleButtonTheme.ActiveHover.FillColor = DefaultTheme.Accent;
        toggleButtonTheme.ActiveHover.BorderColor = DefaultTheme.AccentBorder;

        UI.BeginHBoxContainer("playbackButtons", new(buttonsStartX, buttonsY), 5);
        {
            DrawShuffleButton(isAnyTrackAvailable, buttonSize, controlsLayer, toggleButtonTheme);
            DrawPreviousTrackButton(isAnyTrackAvailable, buttonSize, controlsLayer, iconButtonTheme);
            DrawPlayPauseButton(isAnyTrackAvailable, buttonSize, controlsLayer, iconButtonTheme);
            DrawNextTrackButton(isAnyTrackAvailable, buttonSize, controlsLayer, iconButtonTheme);
            DrawRepeatModeButton(isAnyTrackAvailable, buttonSize, controlsLayer, iconButtonTheme);
        }
        UI.EndHBoxContainer();
    }

    private void DrawShuffleButton(bool isAnyTrackAvailable, Vector2 buttonSize, int controlsLayer, ButtonStylePack toggleButtonTheme)
    {
        bool isShuffleActive = _playbackManager.Mode == PlaybackMode.Shuffle;

        if (UI.Button(
            "shuffle",
            "🔀",
            buttonSize,
            theme: toggleButtonTheme,
            disabled: !isAnyTrackAvailable,
            layer: controlsLayer,
            isActive: isShuffleActive))
        {
            _playbackManager.Mode = isShuffleActive ? PlaybackMode.Sequential : PlaybackMode.Shuffle;
        }
    }

    private void DrawPreviousTrackButton(bool isAnyTrackAvailable, Vector2 buttonSize, int controlsLayer, ButtonStylePack iconButtonTheme)
    {
        if (UI.Button("prevTrack",
            "⏮",
            buttonSize,
            theme: iconButtonTheme,
            disabled: !isAnyTrackAvailable,
            layer: controlsLayer))
        {
            _playbackManager.Previous();
        }
    }

    private void DrawNextTrackButton(bool isAnyTrackAvailable, Vector2 buttonSize, int controlsLayer, ButtonStylePack iconButtonTheme)
    {
        if (UI.Button("nextTrack",
            "⏭",
            buttonSize,
            theme: iconButtonTheme,
            disabled: !isAnyTrackAvailable,
            layer: controlsLayer))
        {
            _playbackManager.Next();
        }
    }

    private void DrawPlayPauseButton(bool isAnyTrackAvailable, Vector2 buttonSize, int controlsLayer, ButtonStylePack iconButtonTheme)
    {
        string playPauseText = _playbackManager.IsPlaying ? "⏸" : "▶";

        if (UI.Button(
            "playPause",
            playPauseText,
            buttonSize,
            theme: iconButtonTheme,
            disabled: !isAnyTrackAvailable,
            layer: controlsLayer))
        {
            _playbackManager.TogglePlayPause();
        }
    }

    private void DrawRepeatModeButton(bool isAnyTrackAvailable, Vector2 buttonSize, int controlsLayer, ButtonStylePack iconButtonTheme)
    {
        string repeatText = _playbackManager.EndAction switch
        {
            PlaybackEndAction.NextInQueue => "🔁",
            PlaybackEndAction.RepeatSong => "🔂",
            PlaybackEndAction.DoNothing => "➡",
            _ => "?"
        };

        if (UI.Button(
            "repeatMode",
            repeatText,
            buttonSize,
            theme: iconButtonTheme,
            disabled: !isAnyTrackAvailable,
            layer: controlsLayer))
        {
            _playbackManager.EndAction = (PlaybackEndAction)(((int)_playbackManager.EndAction + 1) % 3);
        }
    }
}