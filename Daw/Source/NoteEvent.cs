namespace Daw.Core;

/// <summary>
/// Represents a single musical note on the timeline.
/// </summary>
/// <param name="StartTimeMs">The note's start time on the timeline, in milliseconds.</param>
/// <param name="DurationMs">The note's duration, in milliseconds.</param>
/// <param name="Pitch">The MIDI note number (60 = Middle C).</param>
/// <param name="Velocity">The note's velocity (loudness), from 0 to 127.</param>
public record NoteEvent(int StartTimeMs, int DurationMs, int Pitch, int Velocity);