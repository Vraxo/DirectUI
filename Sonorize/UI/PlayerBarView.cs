using DirectUI;
using DirectUI.Core;
using DirectUI.Drawing;
using Sonorize.Audio;
using Sonorize.Sonorize;
using System;
using System.Numerics;

namespace Sonorize;

public class PlayerBarView
{
    private readonly PlaybackManager _playbackManager;
    private readonly Settings _settings;
    private readonly SeekSliderView _seekSliderView;
    private readonly TrackInfoView _trackInfoView;
    private readonly PlaybackControlsView _playbackControlsView;

    public PlayerBarView(PlaybackManager playbackManager, Settings settings)
    {
        _playbackManager = playbackManager;
        _settings = settings;
        _seekSliderView = new SeekSliderView(playbackManager);
        _trackInfoView = new TrackInfoView(playbackManager);
        _playbackControlsView = new PlaybackControlsView(playbackManager);
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

        _trackInfoView.Draw(context, layout.LeftPanelRect);
        DrawCenterPanel(context, layout.CenterPanelRect);
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

    private void DrawCenterPanel(UIContext context, Vortice.Mathematics.Rect panelRect)
    {
        if (panelRect.Width <= 0) return;
        var centerPanelVBoxPos = new Vector2(panelRect.X, panelRect.Y + 8);
        UI.BeginVBoxContainer("centerPanelVBox", centerPanelVBoxPos, 4);
        {
            _playbackControlsView.Draw(context, panelRect.Width);
            _seekSliderView.Draw(context, panelRect.Width);
        }
        UI.EndVBoxContainer();
    }

    private static (Vortice.Mathematics.Rect LeftPanelRect, Vortice.Mathematics.Rect CenterPanelRect, Vortice.Mathematics.Rect RightPanelRect) CalculateLayout(Vortice.Mathematics.Rect controlsRect)
    {
        float leftWidth = Math.Max(180, controlsRect.Width * 0.3f);
        float rightWidth = Math.Max(180, controlsRect.Width * 0.3f);
        float centerWidth = controlsRect.Width - leftWidth - rightWidth;

        if (centerWidth < 300)
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
}```

NEW: Sonorize/Ui/TrackInfoView.cs
Created a new view class to encapsulate the drawing of the currently playing track's album art and text information within the player bar.

```csharp
using System.Numerics;
using DirectUI;
using DirectUI.Core;
using Sonorize.Audio;

namespace Sonorize;

public class TrackInfoView
{
    private readonly PlaybackManager _playbackManager;

    public TrackInfoView(PlaybackManager playbackManager)
    {
        _playbackManager = playbackManager;
    }

    public void Draw(UIContext context, Vortice.Mathematics.Rect panelRect)
    {
        if (panelRect.Width <= 0) return;

        var currentTrack = _playbackManager.CurrentTrack;

        float artSize = 56;
        float padding = (panelRect.Height - artSize) / 2f;
        float gap = 12;

        UI.BeginHBoxContainer("trackInfoHBox", panelRect.TopLeft + new Vector2(padding, padding), gap, VAlignment.Center, fixedRowHeight: artSize);
        {
            DrawAlbumArt(context, currentTrack, artSize);
            DrawTrackText(context, currentTrack, panelRect.Width - artSize - gap - padding * 2);
        }
        UI.EndHBoxContainer();
    }

    private void DrawAlbumArt(UIContext context, MusicFile? currentTrack, float artSize)
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
    }

    private void DrawTrackText(UIContext context, MusicFile? currentTrack, float availableWidth)
    {
        if (availableWidth <= 0) return;

        UI.BeginVBoxContainer("trackTextVBox", UI.Context.Layout.GetCurrentPosition(), 4);
        {
            var title = currentTrack?.Title ?? "No Song Playing";
            var artist = currentTrack?.Artist ?? string.Empty;
            UI.Text("playerTitle", title, new Vector2(availableWidth, 16), new() { FontSize = 14 });
            UI.Text("playerArtist", artist, new Vector2(availableWidth, 12), new() { FontSize = 12, FontColor = new(0.7f, 0.7f, 0.7f, 1f) });
        }
        UI.EndVBoxContainer();
    }
}