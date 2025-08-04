using System.Collections.Generic;

namespace Daw.Core;

/// <summary>
/// Represents a song, containing a timeline of notes and metadata like tempo.
/// </summary>
public class Song
{
    /// <summary>
    /// The tempo of the song in beats per minute (BPM).
    /// </summary>
    public double Tempo { get; set; } = 120.0;

    /// <summary>
    /// The list of note events that make up the song's timeline.
    /// </summary>
    public List<NoteEvent> Events { get; set; } = new();

    /// <summary>
    /// Gets or sets whether playback looping is enabled.
    /// </summary>
    public bool IsLoopingEnabled { get; set; } = false;

    /// <summary>
    /// The start time of the loop region in milliseconds.
    /// </summary>
    public long LoopStartMs { get; set; } = 0;

    /// <summary>
    /// The end time of the loop region in milliseconds.
    /// </summary>
    public long LoopEndMs { get; set; } = 4000; // Default 2 measures at 120bpm
}
