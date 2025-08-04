using System;
using System.Linq;
using Daw.Core;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.Multimedia;

namespace Daw.Audio;

/// <summary>
/// Provides core DAW functionality for converting a Song to MIDI,
/// playing it back, and exporting it as a .mid file.
/// This implementation uses the DryWetMIDI library.
/// Required NuGet package: Melanchall.DryWetMidi
/// </summary>
public class MidiEngine : IDisposable
{
    private Playback? _playback;
    private readonly IOutputDevice? _outputDevice;
    private bool _isDisposed;

    public MidiEngine()
    {
        try
        {
            // Get the default system MIDI output device.
            // This could be "Microsoft GS Wavetable Synth" on Windows.
            _outputDevice = OutputDevice.GetByIndex(0);
        }
        catch (ArgumentException)
        {
            Console.WriteLine("Warning: No MIDI output devices found. Playback will not be available.");
            _outputDevice = null;
        }
    }

    /// <summary>
    /// Converts a Song object into a DryWetMIDI MidiFile object.
    /// </summary>
    /// <param name="song">The song to convert.</param>
    /// <returns>A MidiFile instance representing the song.</returns>
    public MidiFile ConvertToMidiFile(Song song)
    {
        var midiFile = new MidiFile();
        // Tempo is in BPM (beats per minute). Microseconds per quarter note = 60,000,000 / BPM.
        var tempo = new Tempo((long)(60000000 / song.Tempo));
        var tempoMap = TempoMap.Create(tempo);
        // Using a standard time division of 480 ticks per quarter note.
        midiFile.TimeDivision = new TicksPerQuarterNoteTimeDivision(480);

        var trackChunk = new TrackChunk();

        // Add a SetTempo event at the beginning of the track.
        // This is crucial for playback devices to interpret the tick timings correctly.
        trackChunk.Events.Add(new SetTempoEvent(tempo.MicrosecondsPerQuarterNote));

        var notes = song.Events.Select(noteEvent =>
        {
            // Convert milliseconds to MetricTimeSpan
            var startTimeSpan = new MetricTimeSpan(0, 0, 0, noteEvent.StartTimeMs);
            var durationTimeSpan = new MetricTimeSpan(0, 0, 0, noteEvent.DurationMs);

            // Convert metric time to MIDI ticks using the song's tempo map
            long timeInTicks = TimeConverter.ConvertFrom(startTimeSpan, tempoMap);
            long durationInTicks = LengthConverter.ConvertFrom(durationTimeSpan, timeInTicks, tempoMap);

            // Create the note with time and duration in MIDI ticks
            return new Note((SevenBitNumber)noteEvent.Pitch, durationInTicks, timeInTicks)
            {
                Velocity = (SevenBitNumber)noteEvent.Velocity
            };
        });

        // Add the notes to the track chunk.
        // This uses an extension method from Melanchall.DryWetMidi.Interaction.
        trackChunk.AddObjects(notes);

        midiFile.Chunks.Add(trackChunk);
        return midiFile;
    }

    /// <summary>
    /// Exports the given song to a standard MIDI (.mid) file.
    /// </summary>
    /// <param name="song">The song to export.</param>
    /// <param name="filePath">The path to save the .mid file.</param>
    public void ExportToMidiFile(Song song, string filePath)
    {
        var midiFile = ConvertToMidiFile(song);
        midiFile.Write(filePath, overwriteFile: true);
        Console.WriteLine($"Song successfully exported to '{filePath}'.");
    }

    /// <summary>
    /// Plays the given song using the default system MIDI output device.
    /// </summary>
    /// <param name="song">The song to play.</param>
    public void Play(Song song)
    {
        if (_outputDevice == null)
        {
            Console.WriteLine("Cannot play: No MIDI output device is available.");
            return;
        }

        var midiFile = ConvertToMidiFile(song);

        // Stop any previous playback
        Stop();

        _playback = midiFile.GetPlayback(_outputDevice);
        _playback.Start();
    }

    /// <summary>
    /// Stops the current playback, if any.
    /// </summary>
    public void Stop()
    {
        if (_playback != null)
        {
            _playback.Stop();
            _playback.Dispose();
            _playback = null;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_isDisposed)
            return;

        if (disposing)
        {
            Stop();
            _outputDevice?.Dispose();
        }

        _isDisposed = true;
    }
}