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