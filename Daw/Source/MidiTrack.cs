using System.Collections.Generic;

namespace Daw.Core;

/// <summary>
/// Represents a single track in a song, containing its own list of notes and properties.
/// </summary>
public class MidiTrack
{
    public string Name { get; set; } = "New Track";
    public List<NoteEvent> Events { get; set; } = new();
    public OscillatorType OscillatorType { get; set; } = OscillatorType.Sine;

    // Future properties could include: MidiChannel, Instrument, Mute, Solo, etc.

    // Parameterless constructor for serialization
    public MidiTrack() { }

    public MidiTrack(string name)
    {
        Name = name;
    }
}
