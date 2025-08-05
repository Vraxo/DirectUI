using System.Collections.Generic;
using System.Numerics;
using Daw.Core;
using Vortice.Mathematics;

namespace Daw.Views;

/// <summary>
/// Encapsulates the shared state for the piano roll and its child components (like the velocity pane).
/// This separates the data model of the view from the drawing and input handling logic.
/// </summary>
public class PianoRollState
{
    // --- Interaction State ---
    public List<NoteEvent> SelectedNotes { get; } = new();
    public NoteEvent? NoteBeingDragged { get; set; } // The primary note driving the drag
    public NoteEvent? VelocityBarBeingDragged { get; set; }
    public Vector2 DragStartOffset { get; set; }
    public bool IsResizingRight { get; set; }
    public bool IsPanning { get; set; }

    // --- Box Selection State ---
    public bool IsBoxSelecting { get; set; } = false;
    public Vector2 BoxSelectionStart { get; set; }
    public Rect SelectionBox { get; set; }


    // --- Viewport State ---
    public Vector2 PanOffset { get; set; } = Vector2.Zero;
    public float Zoom { get; set; } = 1.0f;

    // Helper to get the single selected note, if there is one.
    public NoteEvent? GetSingleSelectedNote() => SelectedNotes.Count == 1 ? SelectedNotes[0] : null;
}