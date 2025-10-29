using System.Numerics;
using DirectUI;
using Sonorize.Audio;
using System;
using DirectUI.Drawing;
using DirectUI.Core;

namespace Sonorize;

public class PlayerBarView
{
    private readonly PlaybackManager _playbackManager;
    private readonly Settings _settings;
    private bool _isSeekSliderDragging;
    private float _seekSliderValueDuringDrag;

    public PlayerBarView(PlaybackManager playbackManager, Settings settings)
    {
        _playbackManager = playbackManager;
        _settings = settings;
    }

    public void Draw(UIContext context)
    {
        float playerBarHeight = 90f;
        Vector2 windowSize = context.Renderer.RenderTargetSize;
        float controlsY = windowSize.Y - playerBarHeight;
        Vortice.Mathematics.Rect controlsRect = new(0, controlsY, windowSize.X, playerBarHeight);

        var currentTrack = _playbackManager.CurrentTrack;

        DrawBackground(context, controlsRect, currentTrack);

        var layout = CalculateLayout(controlsRect);

        // --- Left Panel: Track Info ---
        DrawTrackInfo(context, currentTrack, layout.LeftPanelRect);

        // --- Center Panel: Playback Controls & Seek ---
        DrawCenterPanel(context, layout.CenterPanelRect);

        // --- Right Panel: Volume ---
        // (Placeholder for volume control)
    }

    private void DrawBackground(UIContext context, Vortice.Mathematics.Rect controlsRect, MusicFile? currentTrack)
    {
        var bgStyle = new BoxStyle
        {
            FillColor = new(24, 24, 24, 255),
            BorderColor = new(40, 40, 40, 255),
            BorderLengthTop = 1f,
            BorderLengthBottom = 0,
            BorderLengthLeft = 0,
            BorderLengthRight = 0,
            Roundness = 0
        };
        context.Renderer.DrawBox(controlsRect, bgStyle);

        if (currentTrack?.AbstractAlbumArt is not null && currentTrack.AbstractAlbumArt.Length > 0)
        {
            string abstractArtKey = currentTrack.FilePath + "_abstract";
            context.Renderer.DrawImage(currentTrack.AbstractAlbumArt, abstractArtKey, controlsRect);

            var overlayStyle = new BoxStyle
            {
                FillColor = new Color(0, 0, 0, (byte)(255 * 0.5f)),
                Roundness = 0,
                BorderLength = 0
            };
            context.Renderer.DrawBox(controlsRect, overlayStyle);
        }
    }

    private void DrawTrackInfo(UIContext context, MusicFile? currentTrack, Vortice.Mathematics.Rect panelRect)
    {
        if (panelRect.Width <= 0) return;

        float artSize = 56;
        float padding = (panelRect.Height - artSize) / 2f;
        float gap = 12;

        UI.BeginHBoxContainer("trackInfoHBox", panelRect.TopLeft + new Vector2(padding, padding), gap, VAlignment.Center, fixedRowHeight: artSize);
        {
            var artPos = UI.Context.Layout.GetCurrentPosition();
            var artRect = new Vortice.Mathematics.Rect(artPos.X, artPos.Y, artSize, artSize);
            if (currentTrack?.AlbumArt is not null)
            {
                context.Renderer.DrawImage(currentTrack.AlbumArt, currentTrack.FilePath, artRect);
            }
            else
            {
                context.Renderer.DrawBox(artRect, new() { FillColor = new(0.2f, 0.2f, 0.2f, 1f), Roundness = 0.1f });
            }
            UI.Context.Layout.AdvanceLayout(new Vector2(artSize, artSize));

            float availableTextWidth = panelRect.Width - artSize - gap - padding * 2;
            if (availableTextWidth > 0)
            {
                UI.BeginVBoxContainer("trackTextVBox", UI.Context.Layout.GetCurrentPosition(), 4);
                {
                    var title = currentTrack?.Title ?? "No Song Playing";
                    var artist = currentTrack?.Artist ?? string.Empty;
                    UI.Text("playerTitle", title, new Vector2(availableTextWidth, 16), new() { FontSize = 14 });
                    UI.Text("playerArtist", artist, new Vector2(availableTextWidth, 12), new() { FontSize = 12, FontColor = new(0.7f, 0.7f, 0.7f, 1f) });
                }
                UI.EndVBoxContainer();
            }
        }
        UI.EndHBoxContainer();
    }

    private void DrawCenterPanel(UIContext context, Vortice.Mathematics.Rect panelRect)
    {
        if (panelRect.Width <= 0) return;
        var centerPanelVBoxPos = new Vector2(panelRect.X, panelRect.Y + 8);
        UI.BeginVBoxContainer("centerPanelVBox", centerPanelVBoxPos, 4);
        {
            DrawControlButtons(context, panelRect.Width);
            DrawSeekSliderWithTimestamps(context, panelRect.Width);
        }
        UI.EndVBoxContainer();
    }

    private void DrawControlButtons(UIContext context, float panelWidth)
    {
        var smallButtonSize = new Vector2(32, 32);
        var playButtonSize = new Vector2(36, 36);

        // Theme for standard icon buttons
        var theme = new ButtonStylePack { FontName = "FontAwesomeSolid", FontSize = 14, Roundness = 1f };
        theme.Normal.FillColor = Colors.Transparent;

        // Theme for toggle buttons (shuffle, repeat)
        var toggleTheme = new ButtonStylePack(theme);
        toggleTheme.Active.FontColor = DefaultTheme.Accent;

        // Theme for the larger play/pause button
        var playTheme = new ButtonStylePack(theme) { FontSize = 22 };


        float gap = 16f;
        float buttonsWidth = (smallButtonSize.X * 4) + playButtonSize.X + (gap * 4);
        float buttonsStartX = (panelWidth - buttonsWidth) / 2;

        UI.BeginHBoxContainer("playbackButtons", new Vector2(buttonsStartX, 0), gap, VAlignment.Center, fixedRowHeight: playButtonSize.Y);
        {
            bool isShuffleActive = _playbackManager.Mode == PlaybackMode.Shuffle;
            if (UI.Button("shuffle", "\uF074", smallButtonSize, theme: toggleTheme, isActive: isShuffleActive)) // fa-random
                _playbackManager.Mode = isShuffleActive ? PlaybackMode.Sequential : PlaybackMode.Shuffle;

            if (UI.Button("prev", "\uF048", smallButtonSize, theme: theme)) _playbackManager.Previous(); // fa-step-backward

            string playIcon = _playbackManager.IsPlaying ? "\uF04C" : "\uF04B"; // fa-pause : fa-play
            if (UI.Button("play", playIcon, playButtonSize, theme: playTheme)) _playbackManager.TogglePlayPause();

            if (UI.Button("next", "\uF051", smallButtonSize, theme: theme)) _playbackManager.Next(); // fa-step-forward

            bool isRepeatActive = _playbackManager.EndAction == PlaybackEndAction.RepeatSong;
            if (UI.Button("repeat", "\uF363", smallButtonSize, theme: toggleTheme, isActive: isRepeatActive)) // fa-repeat
                _playbackManager.EndAction = isRepeatActive ? PlaybackEndAction.NextInQueue : PlaybackEndAction.RepeatSong;
        }
        UI.EndHBoxContainer();
    }

    private void DrawSeekSliderWithTimestamps(UIContext context, float panelWidth)
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

            int sliderId = "seekSlider".GetHashCode();
            bool isDragging = UI.State.ActivelyPressedElementId == sliderId;
            float sliderValue = _isSeekSliderDragging ? _seekSliderValueDuringDrag : (float)currentPos;

            float newValue = UI.HSlider("seekSlider", sliderValue, 0, isLoaded ? (float)totalDuration : 1f, new(sliderWidth, 8),
                disabled: !isLoaded, grabberSize: new(14, 14));

            if (isDragging) _seekSliderValueDuringDrag = newValue;
            if (_isSeekSliderDragging && !isDragging && isLoaded) _playbackManager.Seek(_seekSliderValueDuringDrag);
            _isSeekSliderDragging = isDragging;

            UI.Text("totalTime", totalTimeStr, new(timeLabelWidth, 20), timeStyle, new(HAlignment.Left, VAlignment.Center));
        }
        UI.EndHBoxContainer();
    }

    private static (Vortice.Mathematics.Rect LeftPanelRect, Vortice.Mathematics.Rect CenterPanelRect, Vortice.Mathematics.Rect RightPanelRect) CalculateLayout(Vortice.Mathematics.Rect controlsRect)
    {
        float leftWidth = Math.Max(180, controlsRect.Width * 0.3f);
        float rightWidth = Math.Max(180, controlsRect.Width * 0.3f);
        float centerWidth = controlsRect.Width - leftWidth - rightWidth;

        if (centerWidth < 300) // Adjust if center is too cramped
        {
            float deficit = 300 - centerWidth;
            leftWidth -= deficit / 2;
            rightWidth -= deficit / 2;
            centerWidth = 300;
        }

        return (
            new(controlsRect.Left, controlsRect.Top, leftWidth, controlsRect.Height),
            new(controlsRect.Left + leftWidth, controlsRect.Top, centerWidth, controlsRect.Height),
            new(controlsRect.Left + leftWidth + centerWidth, controlsRect.Top, rightWidth, controlsRect.Height)
        );
    }

    private static string FormatTime(double seconds)
    {
        return (double.IsNaN(seconds) || seconds < 0) ? "00:00" : TimeSpan.FromSeconds(seconds).ToString(@"mm\:ss");
    }
}