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
    private Song? _currentSong; // Store the currently playing song for looping
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
    /// This method should be called every frame to handle real-time updates like looping.
    /// </summary>
    public void Update()
    {
        if (_playback == null || !_playback.IsRunning || _currentSong == null)
        {
            return;
        }

        // Handle custom looping logic by polling the current time.
        if (_currentSong.IsLoopingEnabled && _currentSong.LoopEndMs > _currentSong.LoopStartMs)
        {
            var currentTime = this.CurrentTimeMs;

            // If the current time has passed the loop end point, seek back to the loop start.
            if (currentTime >= _currentSong.LoopEndMs)
            {
                var loopStart = new MetricTimeSpan(0, 0, 0, (int)_currentSong.LoopStartMs);
                _playback.MoveToTime(loopStart);
            }
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
        midiFile.TimeDivision = new TicksPerQuarterNoteTimeDivision(480);

        // A single tempo map is used for the entire file.
        var tempoMap = midiFile.GetTempoMap();
        long microsecondsPerQuarterNote = (long)(60000000 / song.Tempo);
        var setTempoEvent = new SetTempoEvent(microsecondsPerQuarterNote);

        // Create a track for each track in our song
        foreach (var songTrack in song.Tracks)
        {
            var trackChunk = new TrackChunk();

            // Add the tempo event to the first track only
            if (midiFile.Chunks.Count == 0)
            {
                trackChunk.Events.Add(setTempoEvent);
            }

            var notes = songTrack.Events.Select(noteEvent =>
            {
                var startTimeSpan = new MetricTimeSpan(0, 0, 0, noteEvent.StartTimeMs);
                var durationTimeSpan = new MetricTimeSpan(0, 0, 0, noteEvent.DurationMs);

                long timeInTicks = TimeConverter.ConvertTo<MidiTimeSpan>(startTimeSpan, tempoMap).TimeSpan;
                long durationInTicks = LengthConverter.ConvertTo<MidiTimeSpan>(durationTimeSpan, timeInTicks, tempoMap).TimeSpan;

                return new Note((SevenBitNumber)noteEvent.Pitch, durationInTicks, timeInTicks)
                {
                    Velocity = (SevenBitNumber)noteEvent.Velocity
                };
            });

            trackChunk.AddObjects(notes);
            midiFile.Chunks.Add(trackChunk);
        }

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

        _currentSong = song; // Store reference for the update loop
        _playback = midiFile.GetPlayback(_outputDevice);

        // If looping is enabled, we need to decide where to start playing from.
        if (song.IsLoopingEnabled && song.LoopEndMs > song.LoopStartMs)
        {
            var loopStart = new MetricTimeSpan(0, 0, 0, (int)song.LoopStartMs);
            _playback.Start();
            _playback.MoveToTime(loopStart);
        }
        else
        {
            _playback.Start();
        }
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
        _currentSong = null;
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
