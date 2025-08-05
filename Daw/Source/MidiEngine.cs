using System;
using System.Collections.Concurrent;
using System.Linq;
using Daw.Core;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.Multimedia;

namespace Daw.Audio;

/// <summary>
/// Provides core DAW functionality for converting a Song to MIDI,
/// playing it back through the internal AudioEngine, and exporting it as a .mid file.
/// </summary>
public class MidiEngine : IDisposable
{
    private readonly AudioEngine _audioEngine;
    private Playback? _playback;
    private Song? _currentSong;

    // Use a thread-safe queue to handle Note Off events, as they may come from a different thread.
    private readonly ConcurrentQueue<(long releaseTime, Note note)> _notesToRelease = new();

    public bool IsPlaying => _playback?.IsRunning ?? false;
    public long CurrentTimeMs
    {
        get
        {
            return _playback is null || !_playback.IsRunning ? 0 : (long)_playback.GetCurrentTime<MetricTimeSpan>().TotalMilliseconds;
        }
    }

    public MidiEngine(AudioEngine audioEngine)
    {
        _audioEngine = audioEngine ?? throw new ArgumentNullException(nameof(audioEngine));
    }

    /// <summary>
    /// This method should be called every frame to handle real-time updates like looping and note-off events.
    /// </summary>
    public void Update()
    {
        // Clean up any finished voices from the mixer.
        _audioEngine.Update();

        if (_playback == null || !_playback.IsRunning || _currentSong == null)
        {
            // Even when stopped, process any lingering notes to be released
            ProcessNoteReleases(long.MaxValue);
            return;
        }

        long currentTimeMs = CurrentTimeMs;

        ProcessNoteReleases(currentTimeMs);

        if (_currentSong.IsLoopingEnabled && _currentSong.LoopEndMs > _currentSong.LoopStartMs)
        {
            if (currentTimeMs >= _currentSong.LoopEndMs)
            {
                var loopStart = new MetricTimeSpan(0, 0, 0, (int)_currentSong.LoopStartMs);
                _playback.MoveToTime(loopStart);
            }
        }
    }

    private void ProcessNoteReleases(long currentTimeMs)
    {
        while (_notesToRelease.TryPeek(out var item) && item.releaseTime <= currentTimeMs)
        {
            if (_notesToRelease.TryDequeue(out item))
            {
                _audioEngine.NoteOff(item.note.NoteNumber);
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
        var tempoMap = TempoMap.Create(new TicksPerQuarterNoteTimeDivision(480), Tempo.FromBeatsPerMinute(song.Tempo));

        foreach (var songTrack in song.Tracks)
        {
            var trackChunk = new TrackChunk();

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
    /// Plays the given song using the internal AudioEngine.
    /// </summary>
    /// <param name="song">The song to play.</param>
    public void Play(Song song)
    {
        Stop(); // Stop any previous playback first

        var midiFile = ConvertToMidiFile(song);
        _currentSong = song;

        // Use the event-based Playback, which doesn't require an output device.
        _playback = midiFile.GetPlayback();
        _playback.NotesPlaybackStarted += OnNotesPlaybackStarted;
        _playback.NotesPlaybackFinished += OnNotesPlaybackFinished;

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
            // Turn off all notes that might be sustaining by processing the queue
            _notesToRelease.Clear();
            ProcessNoteReleases(long.MaxValue);

            _playback.Stop();
            _playback.NotesPlaybackStarted -= OnNotesPlaybackStarted;
            _playback.NotesPlaybackFinished -= OnNotesPlaybackFinished;
            _playback.Dispose();
            _playback = null;
        }
        _currentSong = null;
    }

    private void OnNotesPlaybackStarted(object? sender, NotesEventArgs e)
    {
        foreach (var note in e.Notes)
        {
            _audioEngine.NoteOn(note.NoteNumber, note.Velocity);
        }
    }

    private void OnNotesPlaybackFinished(object? sender, NotesEventArgs e)
    {
        // This event can fire from a separate thread.
        // Queue the notes to be released safely on the main update thread.
        long currentTimeMs = CurrentTimeMs;
        foreach (var note in e.Notes)
        {
            _notesToRelease.Enqueue((currentTimeMs, note));
        }
    }

    public void Dispose()
    {
        Stop();
    }
}