using System;
using System.Numerics;
using Daw.Core;
using DirectUI;
using Vortice.Mathematics;

namespace Daw.Views;

public class TimelineView
{
    private bool _isDraggingLoop;

    /// <summary>
    /// Draws the timeline grid, loop region, and—when playing—the playback cursor in sync with the audio engine.
    /// </summary>
    public void Draw(
        Rect viewArea,
        Song song,
        Vector2 panOffset,
        float zoom,
        bool isPlaying,
        long currentTimeMs)
    {
        var renderer = UI.Context.Renderer;

        // 1) background panel
        renderer.DrawBox(viewArea, new BoxStyle
        {
            FillColor = DawTheme.PanelBackground,
            Roundness = 0,
            BorderColor = DawTheme.Border,
            BorderLengthBottom = 1
        });

        // 2) the “grid” area (right of the keyboard)
        var grid = new Rect(
            viewArea.X + DawMetrics.KeyboardWidth,
            viewArea.Y,
            viewArea.Width - DawMetrics.KeyboardWidth,
            viewArea.Height);

        HandleInput(grid, song, panOffset, zoom);
        DrawRuler(grid, song, panOffset, zoom);
        DrawLoopRegion(grid, song, panOffset, zoom);

        // 3) **NEW** draw the playhead line using real playback time
        DrawPlaybackCursor(grid, isPlaying, currentTimeMs, panOffset, zoom);
    }

    private void HandleInput(Rect grid, Song song, Vector2 pan, float zoom)
    {
        var inp = UI.Context.InputState;
        var m = inp.MousePosition;

        if (grid.Contains(m) && inp.WasLeftMousePressedThisFrame)
        {
            _isDraggingLoop = true;
            var t = ScreenToTime(m.X, grid, pan, zoom);
            song.LoopStartMs = song.LoopEndMs = (long)t;
        }

        if (!inp.IsLeftMouseDown)
            _isDraggingLoop = false;

        if (_isDraggingLoop)
            song.LoopEndMs = (long)ScreenToTime(m.X, grid, pan, zoom);

        if (!_isDraggingLoop && song.LoopStartMs > song.LoopEndMs)
            (song.LoopEndMs, song.LoopStartMs) = (song.LoopStartMs, song.LoopEndMs);
    }

    private void DrawRuler(Rect grid, Song song, Vector2 pan, float zoom)
    {
        var r = UI.Context.Renderer;
        float pxPerMs = DawMetrics.BasePixelsPerMs * zoom;
        float msPerBeat = 60000f / (float)song.Tempo;
        float pxPerBeat = msPerBeat * pxPerMs;
        if (pxPerBeat < 5) return;

        int beatIdx = (int)Math.Floor(pan.X / pxPerBeat);
        float startX = grid.X - (pan.X % pxPerBeat);
        var textStyle = new ButtonStyle { FontColor = DawTheme.TextDim, FontSize = 10 };

        for (float x = startX; x < grid.Right; x += pxPerBeat, beatIdx++)
        {
            if (x < grid.X) continue;
            bool measure = beatIdx % 4 == 0;
            r.DrawLine(
                new Vector2(x, grid.Y),
                new Vector2(x, grid.Y + (measure ? grid.Height : grid.Height * 0.6f)),
                DawTheme.TextDim,
                1f);

            if (measure)
            {
                string lbl = (beatIdx / 4 + 1).ToString();
                var size = UI.Context.TextService.MeasureText(lbl, textStyle);
                r.DrawText(
                    new Vector2(x - size.X / 2, grid.Y + grid.Height - size.Y),
                    lbl,
                    textStyle,
                    new Alignment(HAlignment.Center, VAlignment.Top),
                    size,
                    DawTheme.TextDim);
            }
        }
    }

    private void DrawLoopRegion(Rect grid, Song song, Vector2 pan, float zoom)
    {
        if (!song.IsLoopingEnabled) return;
        float pxPerMs = DawMetrics.BasePixelsPerMs * zoom;
        float x0 = grid.X + song.LoopStartMs * pxPerMs - pan.X;
        float x1 = grid.X + song.LoopEndMs * pxPerMs - pan.X;
        var loopRect = new Rect(x0, grid.Y, x1 - x0, grid.Height);
        var overlay = new Color4(DawTheme.Accent.R, DawTheme.Accent.G, DawTheme.Accent.B, 0.2f);
        UI.Context.Renderer.DrawBox(loopRect, new BoxStyle { FillColor = overlay, Roundness = 0, BorderLength = 0 });
    }

    private void DrawPlaybackCursor(
        Rect grid,
        bool isPlaying,
        long currentTimeMs,
        Vector2 pan,
        float zoom)
    {
        if (!isPlaying) return;

        float pxPerMs = DawMetrics.BasePixelsPerMs * zoom;
        float x = grid.X + currentTimeMs * pxPerMs - pan.X;
        if (x < grid.X || x > grid.Right) return;

        UI.Context.Renderer.DrawLine(
            new Vector2(x, grid.Y),
            new Vector2(x, grid.Bottom),
            DawTheme.AccentBright,
            2f);
    }

    private float ScreenToTime(float sx, Rect grid, Vector2 pan, float zoom)
        => (sx - grid.X + pan.X) / (DawMetrics.BasePixelsPerMs * zoom);
}
