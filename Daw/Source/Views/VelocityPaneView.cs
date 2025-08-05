using System.Linq;
using System.Numerics;
using Daw.Core;
using DirectUI;
using DirectUI.Input;
using Vortice.Mathematics;

namespace Daw.Views;

/// <summary>
/// A dedicated view responsible for drawing and handling input for the note velocity editor.
/// </summary>
public class VelocityPaneView
{
    public void Draw(Rect viewArea, UIContext context, PianoRollState state, MidiTrack? track, Vector2 panOffset, float zoom)
    {
        // Background and border are drawn by the parent layout (DawAppLogic)
        HandleVelocityInput(context.InputState, viewArea, state, track);
        DrawVelocityBars(context, viewArea, state, track, panOffset, zoom);
    }

    private void HandleVelocityInput(InputState input, Rect velocityArea, PianoRollState state, MidiTrack? track)
    {
        if (track is null) return;

        bool isHoveringVelocity = velocityArea.Contains(input.MousePosition);

        if (isHoveringVelocity && input.WasLeftMousePressedThisFrame)
        {
            var hitNote = HitTestVelocityBars(input.MousePosition, velocityArea, track, state.PanOffset, state.Zoom);
            if (hitNote != null)
            {
                state.VelocityBarBeingDragged = hitNote;
                state.SelectedNote = hitNote; // Also select the note in the main view
            }
        }

        if (!input.IsLeftMouseDown)
        {
            state.VelocityBarBeingDragged = null;
        }

        if (state.VelocityBarBeingDragged != null)
        {
            float mouseY = input.MousePosition.Y;
            float areaY = velocityArea.Y;
            float areaHeight = velocityArea.Height;

            // Calculate velocity (0-127) based on vertical mouse position within the pane
            float ratio = 1.0f - (mouseY - areaY) / areaHeight;
            int newVelocity = (int)(Math.Clamp(ratio, 0f, 1f) * 127);

            state.VelocityBarBeingDragged.Velocity = newVelocity;
        }
    }

    private void DrawVelocityBars(UIContext context, Rect velocityArea, PianoRollState state, MidiTrack track, Vector2 panOffset, float zoom)
    {
        float pixelsPerMs = DawMetrics.BasePixelsPerMs * zoom;
        const float chordOffset = 4f; // Pixels to shift each note in a chord

        // Group notes by start time to handle chords
        var notesByTime = track.Events.GroupBy(n => n.StartTimeMs).OrderBy(g => g.Key);

        foreach (var chord in notesByTime)
        {
            int noteIndexInChord = 0;
            foreach (var note in chord)
            {
                float baseNoteStartPx = (note.StartTimeMs * pixelsPerMs) - panOffset.X;
                float noteStartPx = baseNoteStartPx + (noteIndexInChord * chordOffset);
                float noteEndPx = noteStartPx + 2f; // The bar is just a thin line

                // Culling: Only draw bars for notes that are horizontally visible
                if (noteEndPx < 0 || noteStartPx > velocityArea.Width)
                {
                    noteIndexInChord++;
                    continue;
                }

                float barHeight = (note.Velocity / 127f) * velocityArea.Height;
                var barRect = new Rect(
                    velocityArea.X + noteStartPx,
                    velocityArea.Y + (velocityArea.Height - barHeight), // Anchor to bottom
                    2f, // Fixed width for velocity "lollipops"
                    barHeight
                );

                var color = note == state.SelectedNote ? DawTheme.Selection : DawTheme.AccentBright;

                // The main line of the bar
                context.Renderer.DrawBox(barRect, new BoxStyle { FillColor = color, Roundness = 0 });

                // The "head" of the lollipop
                var headRect = new Rect(velocityArea.X + noteStartPx - 2, barRect.Y - 2, 6, 6);
                context.Renderer.DrawBox(headRect, new BoxStyle { FillColor = color, Roundness = 0.5f });

                noteIndexInChord++;
            }
        }
    }

    private NoteEvent? HitTestVelocityBars(Vector2 screenPos, Rect velocityArea, MidiTrack track, Vector2 panOffset, float zoom)
    {
        float pixelsPerMs = DawMetrics.BasePixelsPerMs * zoom;
        const float chordOffset = 4f;

        var notesByTime = track.Events.GroupBy(n => n.StartTimeMs).OrderBy(g => g.Key);

        // Iterate in reverse to hit-test topmost notes first if they overlap
        foreach (var chord in notesByTime.Reverse())
        {
            int noteIndexInChord = 0;
            foreach (var note in chord)
            {
                float baseNoteStartPx = (note.StartTimeMs * pixelsPerMs) - panOffset.X;
                float noteStartPx = velocityArea.X + baseNoteStartPx + (noteIndexInChord * chordOffset);

                var hitRect = new Rect(noteStartPx - 4, velocityArea.Y, 8, velocityArea.Height); // 8px wide hit area

                if (hitRect.Contains(screenPos))
                {
                    return note;
                }
                noteIndexInChord++;
            }
        }
        return null;
    }
}