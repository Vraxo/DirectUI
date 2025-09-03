using System.Numerics;
using DirectUI;
using Sonorize.Audio;
using System;
using DirectUI.Drawing;

namespace Sonorize;

public class PlaybackControlsView
{
    private readonly PlaybackManager _playbackManager;
    private readonly Settings _settings;
    private bool _isSeekSliderDragging;
    private float _seekSliderValueDuringDrag;

    public PlaybackControlsView(PlaybackManager playbackManager, Settings settings)
    {
        _playbackManager = playbackManager;
        _settings = settings;
    }

    public void Draw(UIContext context)
    {
        // The panel height is now always constant.
        float playbackControlsHeight = 70f;
        Vector2 windowSize = context.Renderer.RenderTargetSize;
        float controlsY = windowSize.Y - playbackControlsHeight;
        Vortice.Mathematics.Rect controlsRect = new(0, controlsY, windowSize.X, playbackControlsHeight);

        var currentTrack = _playbackManager.CurrentTrack;

        // 1. Draw the solid background, which acts as a fallback and provides the top border.
        DrawBackground(context, controlsRect);

        // 2. If available, draw the stretched abstract art on top of the solid background.
        if (currentTrack?.AbstractAlbumArt != null && currentTrack.AbstractAlbumArt.Length > 0)
        {
            // The file path provides a unique key for the renderer's image cache.
            string abstractArtKey = currentTrack.FilePath + "_abstract";
            context.Renderer.DrawImage(currentTrack.AbstractAlbumArt, abstractArtKey, controlsRect);

            // 3. Draw a semi-transparent overlay to dim the art and improve control visibility.
            var overlayStyle = new BoxStyle
            {
                FillColor = new Color(0, 0, 0, (byte)(255 * 0.4f)),
                Roundness = 0,
                BorderLength = 0
            };
            context.Renderer.DrawBox(controlsRect, overlayStyle);
        }

        // 4. Draw the controls layout on top.
        if (_settings.UseCompactPlaybackControls)
        {
            DrawCompactLayout(context, currentTrack, controlsRect);
        }
        else
        {
            DrawStandardLayout(context, currentTrack, controlsRect);
        }
    }

    private void DrawStandardLayout(UIContext context, MusicFile? currentTrack, Vortice.Mathematics.Rect controlsRect)
    {
        float gap = 10;

        UI.BeginHBoxContainer("playbackHBox", controlsRect.TopLeft, gap);
        {
            DrawTrackInfoAndArt(context, currentTrack, controlsRect);
            var leftPanelEndPos = UI.Context.Layout.GetCurrentPosition();
            // Right panel padding is now dynamic based on art size to keep things aligned.
            float rightPanelPadding = _settings.UseLargeAlbumArt ? 0 : 10;
            float centerWidth = controlsRect.Width - leftPanelEndPos.X - rightPanelPadding;
            if (centerWidth > 0)
            {
                DrawStandardControlsAndSlider(context, centerWidth);
            }
        }
        UI.EndHBoxContainer();
    }

    private void DrawCompactLayout(UIContext context, MusicFile? currentTrack, Vortice.Mathematics.Rect controlsRect)
    {
        float gap = 15;
        float rightControlsWidth = 150;
        float leftPadding = _settings.UseLargeAlbumArt ? 0 : 10;

        // --- Left: Album Art (Manually Placed) ---
        float artPadding = _settings.UseLargeAlbumArt ? 0 : 10;
        float artSize = controlsRect.Height - (artPadding * 2);
        var artPos = new Vector2(
            controlsRect.Left + artPadding,
            controlsRect.Top + artPadding
        );
        var artRect = new Vortice.Mathematics.Rect(artPos.X, artPos.Y, artSize, artSize);
        if (currentTrack?.AlbumArt is not null && currentTrack.AlbumArt.Length > 0)
        {
            context.Renderer.DrawImage(currentTrack.AlbumArt, currentTrack.FilePath, artRect);
        }
        else
        {
            context.Renderer.DrawBox(artRect, new() { FillColor = new(0.3f, 0.3f, 0.3f, 1.0f), Roundness = 0.1f });
        }

        // --- Middle Section (Manually Placed & Centered) ---
        float middleSectionStartX = artRect.Right + gap;
        float availableMiddleWidth = controlsRect.Width - middleSectionStartX - rightControlsWidth;

        float middleContentHeight = 36f;
        var middleSectionStartPos = new Vector2(
            middleSectionStartX,
            controlsRect.Top + (controlsRect.Height - middleContentHeight) / 2
        );

        UI.BeginHBoxContainer("compactControlsHBox", middleSectionStartPos, 10);
        {
            DrawCompactControlButtons(context);

            float sliderGroupHeight = 16f + 2f + 8f; // title + gap + slider
            float topSpacer = (middleContentHeight - sliderGroupHeight) / 2f;

            DrawCompactSeekSlider(context, availableMiddleWidth - 120, topSpacer);
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

    private void DrawTrackInfoAndArt(UIContext context, MusicFile? currentTrack, Vortice.Mathematics.Rect panelRect)
    {
        float padding = _settings.UseLargeAlbumArt ? 0 : 10;
        float artSize = panelRect.Height - (padding * 2);

        UI.BeginHBoxContainer("trackInfoHBox", panelRect.TopLeft + new Vector2(padding, padding), padding);
        {
            var artPos = UI.Context.Layout.GetCurrentPosition();
            var artRect = new Vortice.Mathematics.Rect(artPos.X, artPos.Y, artSize, artSize);

            if (currentTrack?.AlbumArt is not null && currentTrack.AlbumArt.Length > 0)
            {
                context.Renderer.DrawImage(currentTrack.AlbumArt, currentTrack.FilePath, artRect);
            }
            else
            {
                context.Renderer.DrawBox(artRect, new() { FillColor = new(0.3f, 0.3f, 0.3f, 1.0f), Roundness = 0.1f });
            }

            UI.Context.Layout.AdvanceLayout(new Vector2(artSize, artSize));

            // Vertically center the text relative to the art
            float textBlockHeight = 16f + 4f + 12f; // font sizes + gap
            float textBlockY = UI.Context.Layout.GetCurrentPosition().Y + (artSize - textBlockHeight) / 2f;

            UI.BeginVBoxContainer("trackInfoVBox", new Vector2(UI.Context.Layout.GetCurrentPosition().X, textBlockY), 4);
            {
                var title = currentTrack?.Title ?? "No song selected";
                var artist = currentTrack?.Artist ?? string.Empty;
                UI.Text("songTitle", title, style: new ButtonStyle { FontSize = 16, FontWeight = Vortice.DirectWrite.FontWeight.SemiBold });
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

    private void DrawStandardControlsAndSlider(UIContext context, float panelWidth)
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

        var smallButtonSize = new Vector2(32, 32);
        var playButtonSize = new Vector2(36, 36);

        var iconButtonTheme = new ButtonStylePack { FontSize = 14f, FontName = "Segoe UI Emoji", Roundness = 0.3f };
        var playButtonTheme = new ButtonStylePack(iconButtonTheme) { Roundness = 1.0f };
        var toggleButtonTheme = new ButtonStylePack(iconButtonTheme);
        toggleButtonTheme.Active.FillColor = DefaultTheme.Accent;

        float buttonsWidth = (smallButtonSize.X * 4) + playButtonSize.X + (4 * 5);
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

    private void DrawCompactControlButtons(UIContext context)
    {
        bool isAnyTrackAvailable = _playbackManager.HasTracks;
        const int controlsLayer = 10;
        var smallButtonSize = new Vector2(32, 32);
        var playButtonSize = new Vector2(36, 36);
        var iconButtonTheme = new ButtonStylePack { FontSize = 14f, FontName = "Segoe UI Emoji", Roundness = 0.3f };
        var playButtonTheme = new ButtonStylePack(iconButtonTheme) { Roundness = 1.0f };

        DrawPreviousTrackButton(isAnyTrackAvailable, smallButtonSize, controlsLayer, iconButtonTheme);
        DrawPlayPauseButton(isAnyTrackAvailable, playButtonSize, controlsLayer, playButtonTheme);
        DrawNextTrackButton(isAnyTrackAvailable, smallButtonSize, controlsLayer, iconButtonTheme);
    }

    private void DrawCompactSeekSlider(UIContext context, float panelWidth, float topSpacer = 0f)
    {
        if (panelWidth < 10) return;

        var currentTrack = _playbackManager.CurrentTrack;
        var title = currentTrack?.Title ?? "No song selected";
        var artist = currentTrack?.Artist ?? string.Empty;
        var trackInfo = string.IsNullOrWhiteSpace(artist) ? title : $"{title} - {artist}";

        UI.BeginVBoxContainer("compactSliderVBox", UI.Context.Layout.GetCurrentPosition(), 2);
        {
            if (topSpacer > 0)
            {
                UI.Text("compactSliderTopSpacer", "", new Vector2(0, topSpacer));
            }
            UI.Text("compactTrackInfo", trackInfo, new Vector2(panelWidth, 16), new ButtonStyle { FontSize = 14 });
            DrawSeekSliderWithTimestamps(context, panelWidth, showTimestamps: false);
        }
        UI.EndVBoxContainer();
    }

    private void DrawSeekSliderWithTimestamps(UIContext context, float panelWidth, bool showTimestamps = true)
    {
        double currentPosition = _playbackManager.CurrentPosition;
        double totalDuration = _playbackManager.TotalDuration;
        bool isAudioLoaded = totalDuration > 0;

        string currentTimeStr = FormatTime(currentPosition);
        string totalTimeStr = FormatTime(totalDuration);

        var timeStyle = new ButtonStyle { FontSize = 12, FontColor = new(0.7f, 0.7f, 0.7f, 1.0f) };
        var timeSize = context.TextService.MeasureText("00:00", timeStyle);
        float timeLabelWidth = showTimestamps ? timeSize.X + 5 : 0;
        float gap = showTimestamps ? 5 : 0;

        float sliderWidth = panelWidth - (timeLabelWidth * 2) - (gap * 2);
        if (sliderWidth < 10) return;

        UI.BeginHBoxContainer("seekHBox", UI.Context.Layout.GetCurrentPosition(), gap);
        {
            if (showTimestamps)
            {
                UI.Text("currentTime", currentTimeStr, new Vector2(timeLabelWidth, 20), timeStyle, new Alignment(HAlignment.Right, VAlignment.Center));
            }

            int seekSliderId = "seekSlider".GetHashCode();
            bool isCurrentlyDragging = UI.State.ActivelyPressedElementId == seekSliderId;
            float sliderInputValue = _isSeekSliderDragging ? _seekSliderValueDuringDrag : (float)currentPosition;
            Vector2 sliderSize = new(sliderWidth, 8);
            ButtonStylePack grabberTheme = new() { Roundness = 1.0f };
            SliderStyle theme = new() { Background = { Roundness = 1 }, Foreground = { FillColor = DefaultTheme.Accent } };

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
                origin: new Vector2(0, showTimestamps ? -6 : 0)
            );

            if (isCurrentlyDragging) _seekSliderValueDuringDrag = newSliderValue;
            if (_isSeekSliderDragging && !isCurrentlyDragging && isAudioLoaded)
            {
                _playbackManager.Seek(_seekSliderValueDuringDrag);
            }
            _isSeekSliderDragging = isCurrentlyDragging;

            if (showTimestamps)
            {
                UI.Text("totalTime", totalTimeStr, new Vector2(timeLabelWidth, 20), timeStyle, new Alignment(HAlignment.Left, VAlignment.Center));
            }
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