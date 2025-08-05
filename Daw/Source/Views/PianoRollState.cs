using System.Numerics;
using Daw.Core;

namespace Daw.Views;

/// <summary>
/// Encapsulates the shared state for the piano roll and its child components (like the velocity pane).
/// This separates the data model of the view from the drawing and input handling logic.
/// </summary>
public class PianoRollState
{
    // --- Interaction State ---
    public NoteEvent? SelectedNote { get; set; }
    public NoteEvent? NoteBeingDragged { get; set; }
    public NoteEvent? VelocityBarBeingDragged { get; set; }
    public Vector2 DragStartOffset { get; set; }
    public bool IsResizingRight { get; set; }
    public bool IsPanning { get; set; }

    // --- Viewport State ---
    public Vector2 PanOffset { get; set; } = Vector2.Zero;
    public float Zoom { get; set; } = 1.0f;
}