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

    // --- Drag state for multi-note move ---
    private Dictionary<NoteEvent, (int StartTime, int Pitch)> _dragStartStates = new();


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
            DrawGrid(gridArea, song);
            DrawNotes(gridArea);
            DrawSelectionBox();
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

    public void DrawVelocityPane(Rect velocityArea, Song song)
    {
        // Delegate drawing and input handling to the specialized child view
        _velocityPaneView.Draw(velocityArea, UI.Context, _state, _activeTrack, song, _state.PanOffset, _state.Zoom);
    }

    private float GetPixelsPerMs()
    {
        if (_song is null || _song.Tempo <= 0) return 0.0f;
        float msPerBeat = (float)(60000.0 / _song.Tempo);
        return (DawMetrics.PixelsPerBeat / msPerBeat) * _state.Zoom;
    }

    private void HandleInput(InputState input, Rect gridArea)
    {
        if (_song is null || _activeTrack is null) return;

        bool isHoveringGrid = gridArea.Contains(input.MousePosition);

        // Panning (Middle Mouse)
        if (input.WasMiddleMousePressedThisFrame && isHoveringGrid) _state.IsPanning = true;
        if (!input.IsMiddleMouseDown) _state.IsPanning = false;
        if (_state.IsPanning) _state.PanOffset += input.MousePosition - input.PreviousMousePosition;

        // Zooming (Scroll Wheel)
        if (isHoveringGrid && input.ScrollDelta != 0)
        {
            _state.Zoom += input.ScrollDelta * 0.1f * _state.Zoom;
            _state.Zoom = Math.Clamp(_state.Zoom, 0.1f, 10f);
        }

        // Left Mouse Button Down
        if (input.WasLeftMousePressedThisFrame && isHoveringGrid)
        {
            HandleLeftClick(input, gridArea);
        }

        // Left Mouse Button Drag
        if (_state.IsBoxSelecting)
        {
            float x = Math.Min(_state.BoxSelectionStart.X, input.MousePosition.X);
            float y = Math.Min(_state.BoxSelectionStart.Y, input.MousePosition.Y);
            float width = Math.Abs(_state.BoxSelectionStart.X - input.MousePosition.X);
            float height = Math.Abs(_state.BoxSelectionStart.Y - input.MousePosition.Y);
            _state.SelectionBox = new Rect(x, y, width, height);
        }

        // Left Mouse Button Up
        if (!input.IsLeftMouseDown)
        {
            if (_state.IsBoxSelecting)
            {
                FinalizeBoxSelection(gridArea);
            }
            _state.IsBoxSelecting = false;
            _state.NoteBeingDragged = null;
            _state.IsResizingRight = false;
            _dragStartStates.Clear();
        }

        // Drag/Resize logic
        if (_currentTool == PianoRollTool.Select)
        {
            HandleDragAndResize(input, gridArea);
        }
    }

    private void FinalizeBoxSelection(Rect gridArea)
    {
        _state.SelectedNotes.Clear();
        if (_activeTrack == null) return;

        float pixelsPerMs = GetPixelsPerMs();
        if (pixelsPerMs <= 0) return;

        foreach (var note in _activeTrack.Events)
        {
            var notePos = GridToScreen(note.StartTimeMs, note.Pitch, gridArea);
            var noteWidth = note.DurationMs * pixelsPerMs;
            var noteRect = new Rect(notePos.X, notePos.Y, noteWidth, DawMetrics.NoteHeight);

            // Manual AABB intersection check
            bool intersects = _state.SelectionBox.Left < noteRect.Right &&
                              _state.SelectionBox.Right > noteRect.Left &&
                              _state.SelectionBox.Top < noteRect.Bottom &&
                              _state.SelectionBox.Bottom > noteRect.Top;

            if (intersects)
            {
                _state.SelectedNotes.Add(note);
            }
        }
    }

    private void HandleLeftClick(InputState input, Rect gridArea)
    {
        var (hitNote, isEdge) = HitTestNotes(input.MousePosition, gridArea);
        bool isShiftHeld = input.HeldKeys.Contains(Keys.Shift);

        switch (_currentTool)
        {
            case PianoRollTool.Select:
                if (hitNote != null)
                {
                    if (isShiftHeld)
                    {
                        // Add or remove from selection with Shift
                        if (_state.SelectedNotes.Contains(hitNote)) _state.SelectedNotes.Remove(hitNote);
                        else _state.SelectedNotes.Add(hitNote);
                    }
                    else
                    {
                        // If not already selected, clear selection and select only this one
                        if (!_state.SelectedNotes.Contains(hitNote))
                        {
                            _state.SelectedNotes.Clear();
                            _state.SelectedNotes.Add(hitNote);
                        }
                    }

                    if (isEdge && _state.SelectedNotes.Count == 1)
                    {
                        _state.IsResizingRight = true;
                    }
                    else
                    {
                        // Prepare for multi-note drag
                        _state.NoteBeingDragged = hitNote;
                        var noteScreenPos = GridToScreen(hitNote.StartTimeMs, hitNote.Pitch, gridArea);
                        _state.DragStartOffset = input.MousePosition - noteScreenPos;
                        _dragStartStates = _state.SelectedNotes.ToDictionary(n => n, n => (n.StartTimeMs, n.Pitch));
                    }
                }
                else
                {
                    // Clicked on empty space
                    if (!isShiftHeld) _state.SelectedNotes.Clear();
                    _state.IsBoxSelecting = true;
                    _state.BoxSelectionStart = input.MousePosition;
                    _state.SelectionBox = new Rect(input.MousePosition.X, input.MousePosition.Y, 0, 0);
                }
                break;

            case PianoRollTool.Pencil:
                if (hitNote != null)
                {
                    _activeTrack?.Events.Remove(hitNote);
                    if (_state.SelectedNotes.Contains(hitNote)) _state.SelectedNotes.Remove(hitNote);
                }
                else
                {
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

        // Multi-note drag
        if (_state.NoteBeingDragged != null)
        {
            var targetNoteScreenPos = input.MousePosition - _state.DragStartOffset;
            var (targetTime, targetPitch) = ScreenToGrid(targetNoteScreenPos, gridArea);

            var dragNoteStart = _dragStartStates[_state.NoteBeingDragged];
            int timeDelta = (int)targetTime - dragNoteStart.StartTime;
            int pitchDelta = targetPitch - dragNoteStart.Pitch;

            foreach (var note in _state.SelectedNotes)
            {
                var originalState = _dragStartStates[note];
                float newTime = originalState.StartTime + timeDelta;
                note.StartTimeMs = (int)(Math.Round(newTime / quantization) * quantization);
                note.Pitch = originalState.Pitch + pitchDelta;
            }
        }

        // Single-note resize
        var singleSelected = _state.GetSingleSelectedNote();
        if (_state.IsResizingRight && singleSelected != null)
        {
            var gridPos = ScreenToGrid(input.MousePosition, gridArea);
            float endEdgeTime = (int)(Math.Round(gridPos.timeMs / quantization) * quantization);
            int newDuration = (int)endEdgeTime - singleSelected.StartTimeMs;
            singleSelected.DurationMs = Math.Max((int)quantization, newDuration);
        }

        // Multi-note deletion
        if (_state.SelectedNotes.Count > 0 && input.PressedKeys.Contains(Keys.Delete))
        {
            foreach (var note in _state.SelectedNotes) _activeTrack?.Events.Remove(note);
            _state.SelectedNotes.Clear();
        }
    }

    private void AddNewNote(Vector2 screenPos, Rect gridArea)
    {
        if (_song == null || _activeTrack == null) return;
        var (time, pitch) = ScreenToGrid(screenPos, gridArea);

        float msPerBeat = (float)(60000.0 / _song.Tempo);
        float msPer16th = msPerBeat / 4;
        time = (int)(Math.Round(time / msPer16th) * msPer16th);

        var newNote = new NoteEvent((int)time, (int)msPer16th * 2, pitch, 100);
        _activeTrack.Events.Add(newNote);
        _state.SelectedNotes.Clear();
        _state.SelectedNotes.Add(newNote);
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

            if (pitch % 12 == 0)
            {
                UI.Text($"key_label_{pitch}", $"C{pitch / 12 - 1}",
                    new Vector2(keyRect.Left + 5, keyRect.Top),
                    new ButtonStyle { FontColor = textColor, FontSize = 10 });
            }
        }
    }

    private void DrawGrid(Rect gridArea, Song song)
    {
        var renderer = UI.Context.Renderer;
        float pixelsPerMs = GetPixelsPerMs();
        if (pixelsPerMs <= 0) return;

        float msPerBeat = (float)(60000.0 / song.Tempo);
        float pitchHeight = gridArea.Height / (DawMetrics.MaxPitch - DawMetrics.MinPitch + 1);
        float pixelsPerBeat = msPerBeat * pixelsPerMs;

        for (int pitch = DawMetrics.MaxPitch; pitch >= DawMetrics.MinPitch; pitch--)
        {
            float y = gridArea.Y + (DawMetrics.MaxPitch - pitch) * pitchHeight;
            var color = _isBlackKey[pitch % 12] ? DawTheme.PanelBackground : DawTheme.Background;
            renderer.DrawBox(new Rect(gridArea.X, y, gridArea.Width, pitchHeight), new BoxStyle { FillColor = color, Roundness = 0 });
        }

        float startX = gridArea.X - (_state.PanOffset.X % pixelsPerBeat);
        int beatIndex = (int)(_state.PanOffset.X / pixelsPerBeat);

        for (float x = startX; x < gridArea.Right; x += pixelsPerBeat)
        {
            bool isMeasure = beatIndex % 4 == 0;
            var color = isMeasure ? DawTheme.PianoRollGridAccent : DawTheme.PianoRollGrid;
            renderer.DrawLine(new Vector2(x, gridArea.Y), new Vector2(x, gridArea.Bottom), color, 1f);
            beatIndex++;
        }

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

        float pixelsPerMs = GetPixelsPerMs();
        if (pixelsPerMs <= 0) return;

        foreach (var note in _activeTrack.Events)
        {
            var noteScreenPos = GridToScreen(note.StartTimeMs, note.Pitch, gridArea);
            float width = note.DurationMs * pixelsPerMs;
            var noteRect = new Rect(noteScreenPos.X, noteScreenPos.Y, width, DawMetrics.NoteHeight);

            if (noteRect.Right < gridArea.X || noteRect.X > gridArea.Right)
                continue;

            bool isSelected = _state.SelectedNotes.Contains(note);
            var style = new BoxStyle
            {
                FillColor = DawTheme.Accent,
                BorderColor = isSelected ? DawTheme.Selection : DawTheme.AccentBright,
                BorderLength = isSelected ? 2f : 1f,
                Roundness = 0.1f
            };
            renderer.DrawBox(noteRect, style);
        }
    }

    private void DrawSelectionBox()
    {
        if (!_state.IsBoxSelecting) return;
        var style = new BoxStyle
        {
            FillColor = new Color4(DawTheme.Selection.R, DawTheme.Selection.G, DawTheme.Selection.B, 0.2f),
            BorderColor = DawTheme.Selection,
            BorderLength = 1f,
            Roundness = 0f
        };
        UI.Context.Renderer.DrawBox(_state.SelectionBox, style);
    }


    private void DrawPlaybackCursor(Rect gridArea, bool isPlaying, long currentTimeMs)
    {
        if (!isPlaying) return;

        float pixelsPerMs = GetPixelsPerMs();
        if (pixelsPerMs <= 0) return;
        float cursorX = gridArea.X + (currentTimeMs * pixelsPerMs) - _state.PanOffset.X;

        if (cursorX < gridArea.X || cursorX > gridArea.Right) return;

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

        float pixelsPerMs = GetPixelsPerMs();
        if (pixelsPerMs <= 0) return (null, false);

        foreach (var note in _activeTrack.Events.AsEnumerable().Reverse())
        {
            var noteScreenPos = GridToScreen(note.StartTimeMs, note.Pitch, gridArea);
            float width = note.DurationMs * pixelsPerMs;
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
        float pixelsPerMs = GetPixelsPerMs();
        float pitchHeight = gridArea.Height / (DawMetrics.MaxPitch - DawMetrics.MinPitch + 1);
        float x = gridArea.X + (timeMs * pixelsPerMs) - _state.PanOffset.X;
        float y = gridArea.Y + ((DawMetrics.MaxPitch - pitch) * pitchHeight) - _state.PanOffset.Y;
        return new Vector2(x, y);
    }

    private (float timeMs, int pitch) ScreenToGrid(Vector2 screenPos, Rect gridArea)
    {
        float pixelsPerMs = GetPixelsPerMs();
        if (pixelsPerMs <= 0) return (0, DawMetrics.MinPitch);

        float pitchHeight = gridArea.Height / (DawMetrics.MaxPitch - DawMetrics.MinPitch + 1);
        float timeMs = (screenPos.X - gridArea.X + _state.PanOffset.X) / pixelsPerMs;
        int pitch = DawMetrics.MaxPitch - (int)((screenPos.Y - gridArea.Y + _state.PanOffset.Y) / pitchHeight);

        timeMs = Math.Max(0, timeMs);
        pitch = Math.Clamp(pitch, DawMetrics.MinPitch, DawMetrics.MaxPitch);

        return (timeMs, pitch);
    }
}