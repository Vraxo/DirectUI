using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Daw.Core;
using DirectUI;
using DirectUI.Core;
using DirectUI.Input;
using Vortice.Mathematics;

namespace Daw.Views;

public class PianoRollView
{
    // --- State ---
    private Song? _song;
    private NoteEvent? _selectedNote;
    private NoteEvent? _noteBeingDragged;
    private Vector2 _dragStartOffset;
    private bool _isResizingRight;
    private bool _isPanning;
    private Vector2 _panOffset = Vector2.Zero;
    private float _zoom = 1.0f; // Multiplier for pixelsPerMs

    private readonly bool[] _isBlackKey = new bool[12];

    // Public accessors for linked views (like Timeline)
    public Vector2 GetPanOffset() => _panOffset;
    public float GetZoom() => _zoom;

    public PianoRollView()
    {
        var blackKeys = new[] { 1, 3, 6, 8, 10 }; // C#, D#, F#, G#, A#
        for (int i = 0; i < 12; i++)
        {
            _isBlackKey[i] = blackKeys.Contains(i);
        }
    }

    public void Draw(Rect viewArea, Song song, bool isPlaying, long currentTimeMs)
    {
        _song = song;
        var renderer = UI.Context.Renderer;
        var input = UI.Context.InputState;

        // Draw panel background
        renderer.DrawBox(viewArea, new BoxStyle { FillColor = DawTheme.PanelBackground, Roundness = 0 });

        var gridArea = new Rect(viewArea.X + DawMetrics.KeyboardWidth, viewArea.Y, viewArea.Width - DawMetrics.KeyboardWidth, viewArea.Height);

        HandleInput(input, gridArea);

        DrawKeyboard(new Rect(viewArea.X, viewArea.Y, DawMetrics.KeyboardWidth, viewArea.Height));
        DrawGrid(gridArea, song.Tempo);
        DrawNotes(gridArea);
        DrawPlaybackCursor(gridArea, isPlaying, currentTimeMs);
    }

    private void HandleInput(InputState input, Rect gridArea)
    {
        if (_song is null) return;

        bool isHoveringGrid = gridArea.Contains(input.MousePosition);

        // Panning
        if (input.WasMiddleMousePressedThisFrame && isHoveringGrid) _isPanning = true;
        if (!input.IsMiddleMouseDown) _isPanning = false;
        if (_isPanning) _panOffset += input.MousePosition - input.PreviousMousePosition;

        // Zooming
        if (isHoveringGrid && input.ScrollDelta != 0)
        {
            _zoom += input.ScrollDelta * 0.1f * _zoom;
            _zoom = Math.Clamp(_zoom, 0.1f, 10f);
        }

        // Note interaction
        var mousePos = input.MousePosition;
        if (input.WasLeftMousePressedThisFrame && isHoveringGrid)
        {
            var hitNote = HitTestNotes(mousePos, gridArea);
            if (hitNote.note != null)
            {
                _selectedNote = hitNote.note;
                if (hitNote.isEdge)
                {
                    _isResizingRight = true;
                }
                else
                {
                    _noteBeingDragged = hitNote.note;
                    var noteScreenPos = GridToScreen(_noteBeingDragged.StartTimeMs, _noteBeingDragged.Pitch, gridArea);
                    _dragStartOffset = mousePos - noteScreenPos;
                }
            }
            else
            {
                _selectedNote = null;
                // Double-click to add a note (simplified with single click + ctrl for now)
                if (input.HeldKeys.Contains(Keys.Control))
                {
                    AddNewNote(mousePos, gridArea);
                }
            }
        }

        if (!input.IsLeftMouseDown)
        {
            _noteBeingDragged = null;
            _isResizingRight = false;
        }

        float msPerBeat = (float)(60000.0 / _song.Tempo);
        float quantization = msPerBeat / 4; // 16th note snapping

        if (_noteBeingDragged != null)
        {
            var targetNoteScreenPos = input.MousePosition - _dragStartOffset;
            var gridPos = ScreenToGrid(targetNoteScreenPos, gridArea);

            float snappedTime = (int)(Math.Round(gridPos.timeMs / quantization) * quantization);

            _noteBeingDragged.StartTimeMs = (int)snappedTime;
            _noteBeingDragged.Pitch = gridPos.pitch;
        }

        if (_isResizingRight && _selectedNote != null)
        {
            var gridPos = ScreenToGrid(mousePos, gridArea);
            float endEdgeTime = (int)(Math.Round(gridPos.timeMs / quantization) * quantization);
            int newDuration = (int)endEdgeTime - _selectedNote.StartTimeMs;
            _selectedNote.DurationMs = Math.Max((int)quantization, newDuration);
        }

        // Deletion
        if (_selectedNote != null && input.PressedKeys.Contains(Keys.Delete))
        {
            _song.Events.Remove(_selectedNote);
            _selectedNote = null;
        }
    }

    private void AddNewNote(Vector2 screenPos, Rect gridArea)
    {
        if (_song == null) return;
        var (time, pitch) = ScreenToGrid(screenPos, gridArea);

        // Snap to nearest 1/16th note
        float msPerBeat = (float)(60000.0 / _song.Tempo);
        float msPer16th = msPerBeat / 4;
        time = (int)(Math.Round(time / msPer16th) * msPer16th);

        var newNote = new NoteEvent((int)time, (int)msPer16th * 2, pitch, 100);
        _song.Events.Add(newNote);
        _selectedNote = newNote;
    }

    private void DrawKeyboard(Rect keyboardArea)
    {
        var renderer = UI.Context.Renderer;
        float pitchHeight = keyboardArea.Height / (DawMetrics.MaxPitch - DawMetrics.MinPitch + 1);

        for (int pitch = DawMetrics.MaxPitch; pitch >= DawMetrics.MinPitch; pitch--)
        {
            float y = keyboardArea.Y + (DawMetrics.MaxPitch - pitch) * pitchHeight;
            var keyRect = new Rect(keyboardArea.X, y, keyboardArea.Width, pitchHeight);

            bool isBlack = _isBlackKey[pitch % 12];
            var keyColor = isBlack ? DawTheme.PianoBlackKey : DawTheme.PianoWhiteKey;
            var textColor = isBlack ? DawTheme.PianoWhiteKey : DawTheme.PianoBlackKey;

            renderer.DrawBox(keyRect, new BoxStyle { FillColor = keyColor, BorderColor = DawTheme.Border, BorderLengthLeft = 0, Roundness = 0 });

            // Draw note names on C keys
            if (pitch % 12 == 0)
            {
                UI.Text($"key_label_{pitch}", $"C{pitch / 12 - 1}",
                    new Vector2(keyRect.Left + 5, keyRect.Top),
                    new ButtonStyle { FontColor = textColor, FontSize = 10 });
            }
        }
    }

    private void DrawGrid(Rect gridArea, double tempo)
    {
        var renderer = UI.Context.Renderer;
        float msPerBeat = (float)(60000.0 / tempo);
        float pitchHeight = gridArea.Height / (DawMetrics.MaxPitch - DawMetrics.MinPitch + 1);
        float pixelsPerMs = DawMetrics.BasePixelsPerMs * _zoom;
        float pixelsPerBeat = msPerBeat * pixelsPerMs;

        // Horizontal lines (Pitches)
        for (int pitch = DawMetrics.MaxPitch; pitch >= DawMetrics.MinPitch; pitch--)
        {
            float y = gridArea.Y + (DawMetrics.MaxPitch - pitch) * pitchHeight;
            var color = _isBlackKey[pitch % 12] ? DawTheme.PanelBackground : DawTheme.Background;
            renderer.DrawBox(new Rect(gridArea.X, y, gridArea.Width, pitchHeight), new BoxStyle { FillColor = color, Roundness = 0 });
        }

        // Vertical lines (Time)
        float startX = gridArea.X - (_panOffset.X % pixelsPerBeat);
        int beatIndex = (int)(_panOffset.X / pixelsPerBeat);

        for (float x = startX; x < gridArea.Right; x += pixelsPerBeat)
        {
            bool isMeasure = beatIndex % 4 == 0;
            var color = isMeasure ? DawTheme.PianoRollGridAccent : DawTheme.PianoRollGrid;
            renderer.DrawLine(new Vector2(x, gridArea.Y), new Vector2(x, gridArea.Bottom), color, 1f);
            beatIndex++;
        }
        
        // Draw Loop Region Overlay
        if (_song != null && _song.IsLoopingEnabled)
        {
            float loopStartX = gridArea.X + (_song.LoopStartMs * pixelsPerMs) - _panOffset.X;
            float loopEndX = gridArea.X + (_song.LoopEndMs * pixelsPerMs) - _panOffset.X;
            var loopRect = new Rect(loopStartX, gridArea.Y, loopEndX - loopStartX, gridArea.Height);
            
            var loopOverlayColor = new Color4(DawTheme.Accent.R, DawTheme.Accent.G, DawTheme.Accent.B, 0.2f);
            renderer.DrawBox(loopRect, new BoxStyle { FillColor = loopOverlayColor, Roundness = 0, BorderLength = 0 });
        }
    }

    private void DrawNotes(Rect gridArea)
    {
        if (_song == null) return;
        var renderer = UI.Context.Renderer;

        foreach (var note in _song.Events)
        {
            var noteScreenPos = GridToScreen(note.StartTimeMs, note.Pitch, gridArea);
            float width = (note.DurationMs * DawMetrics.BasePixelsPerMs * _zoom);
            var noteRect = new Rect(noteScreenPos.X, noteScreenPos.Y, width, DawMetrics.NoteHeight);

            // Culling
            if (noteRect.Right < gridArea.X || noteRect.X > gridArea.Right)
                continue;

            var style = new BoxStyle
            {
                FillColor = DawTheme.Accent,
                BorderColor = note == _selectedNote ? DawTheme.Selection : DawTheme.AccentBright,
                BorderLength = note == _selectedNote ? 2f : 1f,
                Roundness = 0.1f
            };
            renderer.DrawBox(noteRect, style);
        }
    }

    private void DrawPlaybackCursor(Rect gridArea, bool isPlaying, long currentTimeMs)
    {
        if (!isPlaying) return;

        float pixelsPerMs = DawMetrics.BasePixelsPerMs * _zoom;
        float cursorX = gridArea.X + (currentTimeMs * pixelsPerMs) - _panOffset.X;

        // Culling
        if (cursorX < gridArea.X || cursorX > gridArea.Right)
        {
            return;
        }

        var renderer = UI.Context.Renderer;
        renderer.DrawLine(
            new Vector2(cursorX, gridArea.Y),
            new Vector2(cursorX, gridArea.Bottom),
            DawTheme.AccentBright,
            2f);
    }

    private (NoteEvent? note, bool isEdge) HitTestNotes(Vector2 screenPos, Rect gridArea)
    {
        if (_song == null) return (null, false);
        const float edgeWidth = 8f;

        foreach (var note in _song.Events.AsEnumerable().Reverse())
        {
            var noteScreenPos = GridToScreen(note.StartTimeMs, note.Pitch, gridArea);
            float width = (note.DurationMs * DawMetrics.BasePixelsPerMs * _zoom);
            var noteRect = new Rect(noteScreenPos.X, noteScreenPos.Y, width, DawMetrics.NoteHeight);

            if (noteRect.Contains(screenPos))
            {
                bool isEdge = screenPos.X > noteRect.Right - edgeWidth;
                return (note, isEdge);
            }
        }
        return (null, false);
    }

    private Vector2 GridToScreen(float timeMs, int pitch, Rect gridArea)
    {
        float pitchHeight = gridArea.Height / (DawMetrics.MaxPitch - DawMetrics.MinPitch + 1);
        float x = gridArea.X + (timeMs * DawMetrics.BasePixelsPerMs * _zoom) - _panOffset.X;
        float y = gridArea.Y + ((DawMetrics.MaxPitch - pitch) * pitchHeight) - _panOffset.Y;
        return new Vector2(x, y);
    }

    private (float timeMs, int pitch) ScreenToGrid(Vector2 screenPos, Rect gridArea)
    {
        float pitchHeight = gridArea.Height / (DawMetrics.MaxPitch - DawMetrics.MinPitch + 1);
        float timeMs = (screenPos.X - gridArea.X + _panOffset.X) / (DawMetrics.BasePixelsPerMs * _zoom);
        int pitch = DawMetrics.MaxPitch - (int)((screenPos.Y - gridArea.Y + _panOffset.Y) / pitchHeight);

        timeMs = Math.Max(0, timeMs);
        pitch = Math.Clamp(pitch, DawMetrics.MinPitch, DawMetrics.MaxPitch);

        return (timeMs, pitch);
    }
}
