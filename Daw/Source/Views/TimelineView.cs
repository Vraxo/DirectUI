using System;
using System.Numerics;
using Daw.Core;
using DirectUI;
using Vortice.Mathematics;

namespace Daw.Views;

public class TimelineView
{
    private bool _isDraggingLoop;

    public void Draw(Rect viewArea, Song song, Vector2 panOffset, float zoom)
    {
        var renderer = UI.Context.Renderer;

        // Draw background
        renderer.DrawBox(viewArea, new BoxStyle { FillColor = DawTheme.PanelBackground, Roundness = 0, BorderColor = DawTheme.Border, BorderLengthBottom = 1 });

        // Define the interactive grid area, excluding the keyboard space
        var timelineGridArea = new Rect(viewArea.X + DawMetrics.KeyboardWidth, viewArea.Y, viewArea.Width - DawMetrics.KeyboardWidth, viewArea.Height);

        HandleInput(timelineGridArea, song, panOffset, zoom);
        DrawRuler(timelineGridArea, song, panOffset, zoom);
        DrawLoopRegion(timelineGridArea, song, panOffset, zoom);
    }

    private void HandleInput(Rect gridArea, Song song, Vector2 panOffset, float zoom)
    {
        var input = UI.Context.InputState;
        var mousePos = input.MousePosition;
        bool isHovering = gridArea.Contains(mousePos);

        if (isHovering && input.WasLeftMousePressedThisFrame)
        {
            _isDraggingLoop = true;
            // Start dragging, set start point.
            float time = ScreenToTime(mousePos.X, gridArea, panOffset, zoom);
            song.LoopStartMs = (long)time;
            song.LoopEndMs = (long)time; // Collapse to start point initially
        }

        if (!input.IsLeftMouseDown)
        {
            _isDraggingLoop = false;
        }

        if (_isDraggingLoop)
        {
            // Update end point while dragging
            float time = ScreenToTime(mousePos.X, gridArea, panOffset, zoom);
            song.LoopEndMs = (long)time;
        }

        // After dragging is finished, ensure start is always less than end
        if (!_isDraggingLoop && song.LoopStartMs > song.LoopEndMs)
        {
            (song.LoopEndMs, song.LoopStartMs) = (song.LoopStartMs, song.LoopEndMs); // Tuple swap
        }
    }

    private void DrawRuler(Rect gridArea, Song song, Vector2 panOffset, float zoom)
    {
        var renderer = UI.Context.Renderer;
        float pixelsPerMs = DawMetrics.BasePixelsPerMs * zoom;
        float msPerBeat = (float)(60000.0 / song.Tempo);
        float pixelsPerBeat = msPerBeat * pixelsPerMs;
        float pixelsPerMeasure = pixelsPerBeat * 4;

        // Don't draw if beats are too close together
        if (pixelsPerBeat < 5) return;

        // Calculate the first visible beat number based on panning
        int firstBeat = (int)Math.Floor(panOffset.X / pixelsPerBeat);
        float startX = gridArea.X - (panOffset.X % pixelsPerBeat);

        var textStyle = new ButtonStyle { FontColor = DawTheme.TextDim, FontSize = 10 };

        for (float x = startX; x < gridArea.Right; x += pixelsPerBeat, firstBeat++)
        {
            if (x < gridArea.X) continue;

            bool isMeasure = firstBeat % 4 == 0;
            float lineY = isMeasure ? gridArea.Y + gridArea.Height * 0.5f : gridArea.Y + gridArea.Height * 0.75f;
            renderer.DrawLine(new Vector2(x, lineY), new Vector2(x, gridArea.Bottom), DawTheme.PianoRollGridAccent, 1f);

            if (isMeasure)
            {
                int measureNumber = (firstBeat / 4) + 1;
                UI.Text($"measure_label_{measureNumber}", measureNumber.ToString(), new Vector2(x + 3, gridArea.Y + 5), textStyle);
            }
        }
    }

    private void DrawLoopRegion(Rect gridArea, Song song, Vector2 panOffset, float zoom)
    {
        if (!song.IsLoopingEnabled) return;

        float pixelsPerMs = DawMetrics.BasePixelsPerMs * zoom;
        float start = song.LoopStartMs;
        float end = song.LoopEndMs;

        // Handle the case where the user is dragging from right to left
        if (_isDraggingLoop && start > end)
        {
            (end, start) = (start, end);
        }

        float startX = gridArea.X + (start * pixelsPerMs) - panOffset.X;
        float endX = gridArea.X + (end * pixelsPerMs) - panOffset.X;

        var loopRect = new Rect(startX, gridArea.Y, endX - startX, gridArea.Height);

        // Clamp to visible area for drawing
        loopRect.X = Math.Max(loopRect.X, gridArea.X);
        loopRect.Width = Math.Min(endX, gridArea.Right) - loopRect.X;

        if (loopRect.Width > 0)
        {
            var color = new Color4(DawTheme.AccentBright.R, DawTheme.AccentBright.G, DawTheme.AccentBright.B, 0.4f);
            UI.Context.Renderer.DrawBox(loopRect, new BoxStyle { FillColor = color, Roundness = 0 });
        }
    }

    private float ScreenToTime(float screenX, Rect gridArea, Vector2 panOffset, float zoom)
    {
        float pixelsPerMs = DawMetrics.BasePixelsPerMs * zoom;
        float timeMs = (screenX - gridArea.X + panOffset.X) / pixelsPerMs;
        return Math.Max(0, timeMs);
    }
}
