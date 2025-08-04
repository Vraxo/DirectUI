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
}