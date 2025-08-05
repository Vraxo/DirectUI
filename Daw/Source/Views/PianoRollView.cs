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
    // --- State & Child Views ---
    private readonly PianoRollState _state = new();
    private readonly VelocityPaneView _velocityPaneView = new();
    private MidiTrack? _activeTrack;
    private Song? _song;
    private PianoRollTool _currentTool;
    private readonly bool[] _isBlackKey = new bool[12];

    // Public accessors for linked views (like Timeline)
    public Vector2 GetPanOffset() => _state.PanOffset;
    public float GetZoom() => _state.Zoom;

    public PianoRollView()
    {
        var blackKeys = new[] { 1, 3, 6, 8, 10 }; // C#, D#, F#, G#, A#
        for (int i = 0; i < 12; i++)
        {
            _isBlackKey[i] = blackKeys.Contains(i);
        }
    }

    public void Draw(Rect viewArea, MidiTrack? activeTrack, Song song, bool isPlaying, long currentTimeMs, PianoRollTool currentTool)
    {
        _activeTrack = activeTrack;
        _song = song;
        _currentTool = currentTool;
        var renderer = UI.Context.Renderer;
        var input = UI.Context.InputState;

        var gridArea = new Rect(viewArea.X + DawMetrics.KeyboardWidth, viewArea.Y, viewArea.Width - DawMetrics.KeyboardWidth, viewArea.Height);

        HandleInput(input, gridArea);

        if (_activeTrack != null)
        {
            DrawKeyboard(new Rect(viewArea.X, viewArea.Y, DawMetrics.KeyboardWidth, viewArea.Height));
            DrawGrid(gridArea, song.Tempo);
            DrawNotes(gridArea);
            DrawPlaybackCursor(gridArea, isPlaying, currentTimeMs);
        }
        else
        {
            // Center the "No track selected" text
            var textSize = UI.Context.TextService.MeasureText("No track selected. Please add or select a track.", new ButtonStyle());
            var textPos = new Vector2(
                gridArea.X + (gridArea.Width - textSize.X) / 2,
                gridArea.Y + (gridArea.Height - textSize.Y) / 2
            );
            UI.Text("no_track_selected", "No track selected. Please add or select a track.", textPos);
        }
    }

    public void DrawVelocityPane(Rect velocityArea)
    {
        // Delegate drawing and input handling to the specialized child view
        _velocityPaneView.Draw(velocityArea, UI.Context, _state, _activeTrack, _state.PanOffset, _state.Zoom);
    }

    private void HandleInput(InputState input, Rect gridArea)
    {
        if (_song is null || _activeTrack is null) return;

        bool isHoveringGrid = gridArea.Contains(input.MousePosition);

        // Panning (Middle Mouse) should work regardless of the tool
        if (input.WasMiddleMousePressedThisFrame && isHoveringGrid) _state.IsPanning = true;
        if (!input.IsMiddleMouseDown) _state.IsPanning = false;
        if (_state.IsPanning) _state.PanOffset += input.MousePosition - input.PreviousMousePosition;

        // Zooming (Scroll Wheel) should work regardless of the tool
        if (isHoveringGrid && input.ScrollDelta != 0)
        {
            _state.Zoom += input.ScrollDelta * 0.1f * _state.Zoom;
            _state.Zoom = Math.Clamp(_state.Zoom, 0.1f, 10f);
        }

        // --- Tool-Specific Logic (Left Mouse) ---
        if (input.WasLeftMousePressedThisFrame && isHoveringGrid)
        {
            HandleLeftClick(input, gridArea);
        }

        if (!input.IsLeftMouseDown)
        {
            _state.NoteBeingDragged = null;
            _state.IsResizingRight = false;
        }

        // Drag/Resize logic is only for the Select tool
        if (_currentTool == PianoRollTool.Select)
        {
            HandleDragAndResize(input, gridArea);
        }
    }

    private void HandleLeftClick(InputState input, Rect gridArea)
    {
        var (hitNote, isEdge) = HitTestNotes(input.MousePosition, gridArea);

        switch (_currentTool)
        {
            case PianoRollTool.Select:
                if (hitNote != null)
                {
                    _state.SelectedNote = hitNote;
                    if (isEdge)
                    {
                        _state.IsResizingRight = true;
                    }
                    else
                    {
                        _state.NoteBeingDragged = hitNote;
                        var noteScreenPos = GridToScreen(hitNote.StartTimeMs, hitNote.Pitch, gridArea);
                        _state.DragStartOffset = input.MousePosition - noteScreenPos;
                    }
                }
                else
                {
                    _state.SelectedNote = null;
                }
                break;

            case PianoRollTool.Pencil:
                if (hitNote != null)
                {
                    // If clicking an existing note with pencil, delete it
                    _activeTrack?.Events.Remove(hitNote);
                    if (_state.SelectedNote == hitNote) _state.SelectedNote = null;
                }
                else
                {
                    // If clicking empty space, add a new note
                    AddNewNote(input.MousePosition, gridArea);
                }
                break;
        }
    }

    private void HandleDragAndResize(InputState input, Rect gridArea)
    {
        if (_song is null) return;

        float msPerBeat = (float)(60000.0 / _song.Tempo);
        float quantization = msPerBeat / 4; // 16th note snapping

        if (_state.NoteBeingDragged != null)
        {
            var targetNoteScreenPos = input.MousePosition - _state.DragStartOffset;
            var gridPos = ScreenToGrid(targetNoteScreenPos, gridArea);

            float snappedTime = (int)(Math.Round(gridPos.timeMs / quantization) * quantization);

            _state.NoteBeingDragged.StartTimeMs = (int)snappedTime;
            _state.NoteBeingDragged.Pitch = gridPos.pitch;
        }

        if (_state.IsResizingRight && _state.SelectedNote != null)
        {
            var gridPos = ScreenToGrid(input.MousePosition, gridArea);
            float endEdgeTime = (int)(Math.Round(gridPos.timeMs / quantization) * quantization);
            int newDuration = (int)endEdgeTime - _state.SelectedNote.StartTimeMs;
            _state.SelectedNote.DurationMs = Math.Max((int)quantization, newDuration);
        }

        // Deletion
        if (_state.SelectedNote != null && input.PressedKeys.Contains(Keys.Delete))
        {
            _activeTrack?.Events.Remove(_state.SelectedNote);
            _state.SelectedNote = null;
        }
    }

    private void AddNewNote(Vector2 screenPos, Rect gridArea)
    {
        if (_song == null || _activeTrack == null) return;
        var (time, pitch) = ScreenToGrid(screenPos, gridArea);

        // Snap to nearest 1/16th note
        float msPerBeat = (float)(60000.0 / _song.Tempo);
        float msPer16th = msPerBeat / 4;
        time = (int)(Math.Round(time / msPer16th) * msPer16th);

        var newNote = new NoteEvent((int)time, (int)msPer16th * 2, pitch, 100);
        _activeTrack.Events.Add(newNote);
        _state.SelectedNote = newNote;
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
        float pixelsPerMs = DawMetrics.BasePixelsPerMs * _state.Zoom;
        float pixelsPerBeat = msPerBeat * pixelsPerMs;

        // Horizontal lines (Pitches)
        for (int pitch = DawMetrics.MaxPitch; pitch >= DawMetrics.MinPitch; pitch--)
        {
            float y = gridArea.Y + (DawMetrics.MaxPitch - pitch) * pitchHeight;
            var color = _isBlackKey[pitch % 12] ? DawTheme.PanelBackground : DawTheme.Background;
            renderer.DrawBox(new Rect(gridArea.X, y, gridArea.Width, pitchHeight), new BoxStyle { FillColor = color, Roundness = 0 });
        }

        // Vertical lines (Time)
        float startX = gridArea.X - (_state.PanOffset.X % pixelsPerBeat);
        int beatIndex = (int)(_state.PanOffset.X / pixelsPerBeat);

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
            float loopStartX = gridArea.X + (_song.LoopStartMs * pixelsPerMs) - _state.PanOffset.X;
            float loopEndX = gridArea.X + (_song.LoopEndMs * pixelsPerMs) - _state.PanOffset.X;
            var loopRect = new Rect(loopStartX, gridArea.Y, loopEndX - loopStartX, gridArea.Height);

            var loopOverlayColor = new Color4(DawTheme.Accent.R, DawTheme.Accent.G, DawTheme.Accent.B, 0.2f);
            renderer.DrawBox(loopRect, new BoxStyle { FillColor = loopOverlayColor, Roundness = 0, BorderLength = 0 });
        }
    }

    private void DrawNotes(Rect gridArea)
    {
        if (_song == null || _activeTrack == null) return;
        var renderer = UI.Context.Renderer;

        foreach (var note in _activeTrack.Events)
        {
            var noteScreenPos = GridToScreen(note.StartTimeMs, note.Pitch, gridArea);
            float width = (note.DurationMs * DawMetrics.BasePixelsPerMs * _state.Zoom);
            var noteRect = new Rect(noteScreenPos.X, noteScreenPos.Y, width, DawMetrics.NoteHeight);

            // Culling
            if (noteRect.Right < gridArea.X || noteRect.X > gridArea.Right)
                continue;

            var style = new BoxStyle
            {
                FillColor = DawTheme.Accent,
                BorderColor = note == _state.SelectedNote ? DawTheme.Selection : DawTheme.AccentBright,
                BorderLength = note == _state.SelectedNote ? 2f : 1f,
                Roundness = 0.1f
            };
            renderer.DrawBox(noteRect, style);
        }
    }

    private void DrawPlaybackCursor(Rect gridArea, bool isPlaying, long currentTimeMs)
    {
        if (!isPlaying) return;

        float pixelsPerMs = DawMetrics.BasePixelsPerMs * _state.Zoom;
        float cursorX = gridArea.X + (currentTimeMs * pixelsPerMs) - _state.PanOffset.X;

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
        if (_song == null || _activeTrack == null) return (null, false);
        const float edgeWidth = 8f;

        foreach (var note in _activeTrack.Events.AsEnumerable().Reverse())
        {
            var noteScreenPos = GridToScreen(note.StartTimeMs, note.Pitch, gridArea);
            float width = (note.DurationMs * DawMetrics.BasePixelsPerMs * _state.Zoom);
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
        float x = gridArea.X + (timeMs * DawMetrics.BasePixelsPerMs * _state.Zoom) - _state.PanOffset.X;
        float y = gridArea.Y + ((DawMetrics.MaxPitch - pitch) * pitchHeight) - _state.PanOffset.Y;
        return new Vector2(x, y);
    }

    private (float timeMs, int pitch) ScreenToGrid(Vector2 screenPos, Rect gridArea)
    {
        float pitchHeight = gridArea.Height / (DawMetrics.MaxPitch - DawMetrics.MinPitch + 1);
        float timeMs = (screenPos.X - gridArea.X + _state.PanOffset.X) / (DawMetrics.BasePixelsPerMs * _state.Zoom);
        int pitch = DawMetrics.MaxPitch - (int)((screenPos.Y - gridArea.Y + _state.PanOffset.Y) / pitchHeight);

        timeMs = Math.Max(0, timeMs);
        pitch = Math.Clamp(pitch, DawMetrics.MinPitch, DawMetrics.MaxPitch);

        return (timeMs, pitch);
    }
}