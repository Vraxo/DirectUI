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

    public bool IsPlaying => _playback?.IsRunning ?? false;
    public long CurrentTimeMs
    {
        get
        {
            return _playback is null || !_playback.IsRunning ? 0 : (long)_playback.GetCurrentTime<MetricTimeSpan>().TotalMilliseconds;
        }
    }

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
        // Step 1: Create the structure of the MIDI file with tempo information.
        var midiFile = new MidiFile();
        // Using a standard time division of 480 ticks per quarter note.
        midiFile.TimeDivision = new TicksPerQuarterNoteTimeDivision(480);
        var trackChunk = new TrackChunk();

        // Tempo is in BPM (beats per minute). Microseconds per quarter note = 60,000,000 / BPM.
        long microsecondsPerQuarterNote = (long)(60000000 / song.Tempo);
        trackChunk.Events.Add(new SetTempoEvent(microsecondsPerQuarterNote));
        midiFile.Chunks.Add(trackChunk);

        // Step 2: Get the tempo map from the file itself.
        // This ensures the tempo map uses the file's time division, which is critical for sync.
        var tempoMap = midiFile.GetTempoMap();

        // Step 3: Convert song events to notes using the correct tempo map.
        var notes = song.Events.Select(noteEvent =>
        {
            var startTimeSpan = new MetricTimeSpan(0, 0, 0, noteEvent.StartTimeMs);
            var durationTimeSpan = new MetricTimeSpan(0, 0, 0, noteEvent.DurationMs);

            // Use the older ConvertTo<> that the user's compiler accepts.
            long timeInTicks = TimeConverter.ConvertTo<MidiTimeSpan>(startTimeSpan, tempoMap).TimeSpan;
            long durationInTicks = LengthConverter.ConvertTo<MidiTimeSpan>(durationTimeSpan, timeInTicks, tempoMap).TimeSpan;

            // Create the note with time and duration in MIDI ticks
            return new Note((SevenBitNumber)noteEvent.Pitch, durationInTicks, timeInTicks)
            {
                Velocity = (SevenBitNumber)noteEvent.Velocity
            };
        });

        // Step 4: Add the created notes to the track chunk.
        trackChunk.AddObjects(notes);

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
        
        // Apply looping if it's enabled and the range is valid.
        if (song.IsLoopingEnabled && song.LoopEndMs > song.LoopStartMs)
        {
            var loopStart = new MetricTimeSpan(0, 0, 0, (int)song.LoopStartMs);
            var loopEnd = new MetricTimeSpan(0, 0, 0, (int)song.LoopEndMs);
            _playback.Loop = new Loop(loopStart, loopEnd);
        }
        
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
