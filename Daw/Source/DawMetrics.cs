namespace Daw.Views;

/// <summary>
/// Defines shared constants for layout, grid, and pitch to ensure consistency
/// between the Timeline, Piano Roll, and other views.
/// </summary>
public static class DawMetrics
{
    // --- Layout ---
    public const float TopBarHeight = 70;
    public const float TimelineHeight = 40;
    public const float PianoRollToolbarHeight = 40;
    public const float KeyboardWidth = 80;

    // --- Grid & Zoom ---
    public const float BasePixelsPerMs = 0.1f;
    public const float NoteHeight = 20;

    // --- Pitch Range ---
    public const int MinPitch = 24; // C1
    public const int MaxPitch = 108; // C8
}
