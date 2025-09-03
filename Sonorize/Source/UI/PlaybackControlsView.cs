using System.Numerics;
using DirectUI;
using Sonorize.Audio;
using System;

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
        Vector2 windowSize = context.Renderer.RenderTargetSize;
        float controlsY = windowSize.Y - playbackControlsHeight;
        Vortice.Mathematics.Rect controlsRect = new(0, controlsY, windowSize.X, playbackControlsHeight);

        DrawBackground(context, controlsRect);

        var currentTrack = _playbackManager.CurrentTrack;
        float gap = 10;
        float rightPanelPadding = 10;

        // Use HBox to lay out the main sections: Info | Controls
        UI.BeginHBoxContainer("playbackHBox", new Vector2(0, controlsY), gap);
        {
            var leftPanelStartPos = UI.Context.Layout.GetCurrentPosition();

            // --- Left Panel: Album Art & Track Info ---
            // This now allows its size to be determined by its content.
            DrawTrackInfoAndArt(context, currentTrack);

            var leftPanelEndPos = UI.Context.Layout.GetCurrentPosition();

            // --- Center Panel: Buttons & Seek Slider ---
            // Dynamically calculate the remaining width for the center panel.
            float centerWidth = windowSize.X - leftPanelEndPos.X - rightPanelPadding;
            if (centerWidth > 0)
            {
                DrawPlaybackControlsAndSlider(context, centerWidth);
            }
        }
        UI.EndHBoxContainer();
    }

    private static void DrawBackground(UIContext context, Vortice.Mathematics.Rect controlsRect)
    {
        context.Renderer.DrawBox(controlsRect, new()
        {
            FillColor = new(30 / 255f, 30 / 255f, 30 / 255f, 1.0f), // Darker background
            BorderColor = new(45 / 255f, 45 / 255f, 45 / 255f, 1.0f),
            BorderLengthTop = 1f,
            BorderLengthBottom = 0,
            BorderLengthLeft = 0,
            BorderLengthRight = 0,
            Roundness = 0
        });
    }

    private static void DrawTrackInfoAndArt(UIContext context, MusicFile? currentTrack)
    {
        float padding = 10f;
        float artSize = 50f;
        // The VBox is now a child of the main HBox, so its position is handled automatically.
        UI.BeginHBoxContainer("trackInfoHBox", UI.Context.Layout.GetCurrentPosition() + new Vector2(padding, padding), padding);
        {
            // Album Art
            var artPos = UI.Context.Layout.GetCurrentPosition();
            var artRect = new Vortice.Mathematics.Rect(artPos.X, artPos.Y, artSize, artSize);

            if (currentTrack?.AlbumArt is not null && currentTrack.AlbumArt.Length > 0)
            {
                // Use file path as a unique key for caching the decoded image
                context.Renderer.DrawImage(currentTrack.AlbumArt, currentTrack.FilePath, artRect);
            }
            else
            {
                // Placeholder for Album Art
                context.Renderer.DrawBox(artRect, new() { FillColor = new(0.3f, 0.3f, 0.3f, 1.0f), Roundness = 0.1f });
            }

            UI.Context.Layout.AdvanceLayout(new Vector2(artSize, artSize));


            // Track Info VBox
            UI.BeginVBoxContainer("trackInfoVBox", UI.Context.Layout.GetCurrentPosition(), 4);
            {
                var title = currentTrack?.Title ?? "No song selected";
                var artist = currentTrack?.Artist ?? string.Empty;

                // Song Title (larger font)
                UI.Text("songTitle", title, style: new ButtonStyle { FontSize = 16, FontWeight = Vortice.DirectWrite.FontWeight.SemiBold });

                // Artist Name (smaller, dimmer font)
                if (!string.IsNullOrEmpty(artist))
                {
                    var artistStyle = new ButtonStyle { FontSize = 12, FontColor = new(0.7f, 0.7f, 0.7f, 1.0f) };
                    UI.Text("artistName", artist, style: artistStyle);
                }
            }
            UI.EndVBoxContainer();
        }
        UI.EndHBoxContainer();
    }


    private void DrawPlaybackControlsAndSlider(UIContext context, float panelWidth)
    {
        var startPos = UI.Context.Layout.GetCurrentPosition();

        UI.BeginVBoxContainer("centerControlsVBox", startPos, 2);
        {
            DrawControlButtons(context, panelWidth);
            DrawSeekSliderWithTimestamps(context, panelWidth);
        }
        UI.EndVBoxContainer();
    }

    private void DrawControlButtons(UIContext context, float panelWidth)
    {
        bool isAnyTrackAvailable = _playbackManager.HasTracks;
        const int controlsLayer = 10;

        // Button styles
        var smallButtonSize = new Vector2(32, 32);
        var playButtonSize = new Vector2(36, 36);

        var iconButtonTheme = new ButtonStylePack
        {
            FontSize = 14f,
            FontName = "Segoe UI Emoji",
            Roundness = 0.3f
        };
        var playButtonTheme = new ButtonStylePack(iconButtonTheme) { Roundness = 1.0f };
        var toggleButtonTheme = new ButtonStylePack(iconButtonTheme);
        toggleButtonTheme.Active.FillColor = DefaultTheme.Accent;

        // HBox for centering
        float buttonsWidth = (smallButtonSize.X * 4) + playButtonSize.X + (4 * 5); // 4 small, 1 large, 4 gaps
        float buttonsStartX = (panelWidth - buttonsWidth) / 2;
        var hboxPos = new Vector2(UI.Context.Layout.GetCurrentPosition().X + buttonsStartX, UI.Context.Layout.GetCurrentPosition().Y + 5);

        UI.BeginHBoxContainer("playbackButtons", hboxPos, 5);
        {
            DrawShuffleButton(isAnyTrackAvailable, smallButtonSize, controlsLayer, toggleButtonTheme);
            DrawPreviousTrackButton(isAnyTrackAvailable, smallButtonSize, controlsLayer, iconButtonTheme);
            DrawPlayPauseButton(isAnyTrackAvailable, playButtonSize, controlsLayer, playButtonTheme);
            DrawNextTrackButton(isAnyTrackAvailable, smallButtonSize, controlsLayer, iconButtonTheme);
            DrawRepeatModeButton(isAnyTrackAvailable, smallButtonSize, controlsLayer, iconButtonTheme);
        }
        UI.EndHBoxContainer();
    }


    private void DrawSeekSliderWithTimestamps(UIContext context, float panelWidth)
    {
        double currentPosition = _playbackManager.CurrentPosition;
        double totalDuration = _playbackManager.TotalDuration;
        bool isAudioLoaded = totalDuration > 0;

        string currentTimeStr = FormatTime(currentPosition);
        string totalTimeStr = FormatTime(totalDuration);

        var timeStyle = new ButtonStyle { FontSize = 12, FontColor = new(0.7f, 0.7f, 0.7f, 1.0f) };
        var timeSize = context.TextService.MeasureText("00:00", timeStyle);
        float timeLabelWidth = timeSize.X + 5;
        float gap = 5; // Define a gap for spacing

        float sliderWidth = panelWidth - (timeLabelWidth * 2) - (gap * 2); // Account for two gaps
        if (sliderWidth < 10) return; // Don't draw if there's no space

        // HBox to hold [Time, Slider, Time]
        UI.BeginHBoxContainer("seekHBox", UI.Context.Layout.GetCurrentPosition(), gap); // Use the gap
        {
            // Current Time
            UI.Text("currentTime", currentTimeStr, new Vector2(timeLabelWidth, 20), timeStyle, new Alignment(HAlignment.Right, VAlignment.Center));

            // --- The Slider ---
            int seekSliderId = "seekSlider".GetHashCode();
            bool isCurrentlyDragging = UI.State.ActivelyPressedElementId == seekSliderId;
            float sliderInputValue = _isSeekSliderDragging ? _seekSliderValueDuringDrag : (float)currentPosition;
            Vector2 sliderSize = new(sliderWidth, 8); // Thinner slider track
            ButtonStylePack grabberTheme = new() { Roundness = 1.0f };
            SliderStyle theme = new() { Background = { Roundness = 1 }, Foreground = { FillColor = DefaultTheme.Accent } };

            // To vertically center the 14px grabber in the 20px text row, we need to shift the slider's 8px track.
            // Grabber top = slider_y + (track_height/2) - (grabber_height/2) = slider_y + 4 - 7 = slider_y - 3
            // Desired grabber top = row_y + (row_height - grabber_height)/2 = row_y + (20 - 14)/2 = row_y + 3
            // So, slider_y - 3 = row_y + 3  =>  slider_y = row_y + 6.
            // We shift the slider down by 6px using the origin property.
            float newSliderValue = UI.HSlider(
                id: "seekSlider",
                currentValue: sliderInputValue,
                minValue: 0f,
                maxValue: isAudioLoaded ? (float)totalDuration : 1.0f,
                size: sliderSize,
                disabled: !isAudioLoaded,
                grabberSize: new(14, 14),
                grabberTheme: grabberTheme,
                theme: theme,
                origin: new Vector2(0, -6) // Shift down to visually center with text
            );

            if (isCurrentlyDragging) _seekSliderValueDuringDrag = newSliderValue;
            if (_isSeekSliderDragging && !isCurrentlyDragging && isAudioLoaded)
            {
                _playbackManager.Seek(_seekSliderValueDuringDrag);
            }
            _isSeekSliderDragging = isCurrentlyDragging;
            // --- End Slider ---


            // Total Time
            UI.Text("totalTime", totalTimeStr, new Vector2(timeLabelWidth, 20), timeStyle, new Alignment(HAlignment.Left, VAlignment.Center));
        }
        UI.EndHBoxContainer();
    }


    #region Button Drawing Helpers
    private void DrawShuffleButton(bool isAnyTrackAvailable, Vector2 buttonSize, int controlsLayer, ButtonStylePack toggleButtonTheme)
    {
        bool isShuffleActive = _playbackManager.Mode == PlaybackMode.Shuffle;
        if (UI.Button("shuffle", "🔀", buttonSize, theme: toggleButtonTheme, disabled: !isAnyTrackAvailable, layer: controlsLayer, isActive: isShuffleActive))
        {
            _playbackManager.Mode = isShuffleActive ? PlaybackMode.Sequential : PlaybackMode.Shuffle;
        }
    }

    private void DrawPreviousTrackButton(bool isAnyTrackAvailable, Vector2 buttonSize, int controlsLayer, ButtonStylePack iconButtonTheme)
    {
        if (UI.Button("prevTrack", "⏮", buttonSize, theme: iconButtonTheme, disabled: !isAnyTrackAvailable, layer: controlsLayer))
        {
            _playbackManager.Previous();
        }
    }

    private void DrawPlayPauseButton(bool isAnyTrackAvailable, Vector2 buttonSize, int controlsLayer, ButtonStylePack iconButtonTheme)
    {
        string playPauseText = _playbackManager.IsPlaying ? "⏸" : "▶";
        if (UI.Button("playPause", playPauseText, buttonSize, theme: iconButtonTheme, disabled: !isAnyTrackAvailable, layer: controlsLayer))
        {
            _playbackManager.TogglePlayPause();
        }
    }

    private void DrawNextTrackButton(bool isAnyTrackAvailable, Vector2 buttonSize, int controlsLayer, ButtonStylePack iconButtonTheme)
    {
        if (UI.Button("nextTrack", "⏭", buttonSize, theme: iconButtonTheme, disabled: !isAnyTrackAvailable, layer: controlsLayer))
        {
            _playbackManager.Next();
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
        if (UI.Button("repeatMode", repeatText, buttonSize, theme: iconButtonTheme, disabled: !isAnyTrackAvailable, layer: controlsLayer))
        {
            _playbackManager.EndAction = (PlaybackEndAction)(((int)_playbackManager.EndAction + 1) % 3);
        }
    }
    #endregion

    private static string FormatTime(double seconds)
    {
        if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0)
        {
            return "00:00";
        }
        var timeSpan = TimeSpan.FromSeconds(seconds);
        return timeSpan.ToString(@"mm\:ss");
    }
}