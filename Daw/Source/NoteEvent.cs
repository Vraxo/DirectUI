namespace Daw.Core;

/// <summary>
/// Represents a single musical note on the timeline.
/// Now a mutable class to allow for interactive editing.
/// </summary>
public class NoteEvent
{
    public int StartTimeMs { get; set; }
    public int DurationMs { get; set; }
    public int Pitch { get; set; }
    public int Velocity { get; set; }

    public NoteEvent(int startTimeMs, int durationMs, int pitch, int velocity)
    {
        StartTimeMs = startTimeMs;
        DurationMs = durationMs;
        Pitch = pitch;
        Velocity = velocity;
    }
}