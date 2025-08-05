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
/// playing it through the internal AudioEngine, and exporting it as a .mid file.
/// </summary>
public class MidiEngine : IDisposable
{
    private readonly AudioEngine _audioEngine;
    private Playback? _playback;
    private Song? _currentSong;

    public bool IsPlaying => _playback?.IsRunning ?? false;
    public long CurrentTimeMs
    {
        get
        {
            if (_playback is null || !_playback.IsRunning) return 0;

            // For looped playback, we need to add the loop start time back to get the correct timeline position
            if (_currentSong is { IsLoopingEnabled: true } && _currentSong.LoopEndMs > _currentSong.LoopStartMs)
            {
                long loopRelativeTime = (long)_playback.GetCurrentTime<MetricTimeSpan>().TotalMilliseconds;
                return _currentSong.LoopStartMs + loopRelativeTime;
            }

            return (long)_playback.GetCurrentTime<MetricTimeSpan>().TotalMilliseconds;
        }
    }

    public MidiEngine(AudioEngine audioEngine)
    {
        _audioEngine = audioEngine ?? throw new ArgumentNullException(nameof(audioEngine));
    }

    /// <summary>
    /// This method should be called every frame to handle non-time-critical updates like audio engine cleanup.
    /// </summary>
    public void Update()
    {
        // Clean up any finished voices from the mixer. This is not time-critical.
        _audioEngine.Update();
    }

    /// <summary>
    /// Converts a Song object into a DryWetMIDI MidiFile object.
    /// </summary>
    /// <param name="song">The song to convert.</param>
    /// <returns>A MidiFile instance representing the song.</returns>
    public MidiFile ConvertToMidiFile(Song song)
    {
        var midiFile = new MidiFile();
        // --- FIX: Explicitly set the Time Division on the MidiFile object ---
        // This is crucial for DryWetMidi to correctly interpret the tick values later.
        var timeDivision = new TicksPerQuarterNoteTimeDivision(480);
        midiFile.TimeDivision = timeDivision;

        var tempoMap = TempoMap.Create(timeDivision, Tempo.FromBeatsPerMinute(song.Tempo));

        // CRITICAL FIX: The MIDI file must contain a SetTempoEvent for the playback engine to know the correct speed.
        // We create a "conductor track" for this and other meta-events.
        var conductorTrack = new TrackChunk();
        conductorTrack.Events.Add(new SetTempoEvent(Tempo.FromBeatsPerMinute(song.Tempo).MicrosecondsPerQuarterNote));
        midiFile.Chunks.Add(conductorTrack);

        for (int i = 0; i < song.Tracks.Count; i++)
        {
            if (i >= 16)
            {
                Console.WriteLine($"Warning: Skipping track {i+1} ('{song.Tracks[i].Name}') as it exceeds the 16-channel MIDI limit.");
                continue;
            }
        
            var songTrack = song.Tracks[i];
            var trackChunk = new TrackChunk();

            var notes = songTrack.Events.Select(noteEvent =>
            {
                var startTimeSpan = new MetricTimeSpan(0, 0, 0, noteEvent.StartTimeMs);
                var durationTimeSpan = new MetricTimeSpan(0, 0, 0, noteEvent.DurationMs);

                long timeInTicks = TimeConverter.ConvertTo<MidiTimeSpan>(startTimeSpan, tempoMap).TimeSpan;
                long durationInTicks = LengthConverter.ConvertTo<MidiTimeSpan>(durationTimeSpan, timeInTicks, tempoMap).TimeSpan;

                return new Note((SevenBitNumber)noteEvent.Pitch, durationInTicks, timeInTicks)
                {
                    Velocity = (SevenBitNumber)noteEvent.Velocity,
                    Channel = (FourBitNumber)i
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
        Stop();

        var midiFile = ConvertToMidiFile(song);
        _currentSong = song;

        Playback playback;

        if (song.IsLoopingEnabled && song.LoopEndMs > song.LoopStartMs)
        {
            var tempoMap = midiFile.GetTempoMap();
            var loopStart = new MetricTimeSpan(0, 0, 0, (int)song.LoopStartMs);
            var loopEnd = new MetricTimeSpan(0, 0, 0, (int)song.LoopEndMs);
            long loopStartTimeTicks = TimeConverter.ConvertFrom(loopStart, tempoMap);
            long loopEndTimeTicks = TimeConverter.ConvertFrom(loopEnd, tempoMap);

            var loopedTrackChunk = new TrackChunk();
            loopedTrackChunk.Events.Add(new SetTempoEvent(Tempo.FromBeatsPerMinute(song.Tempo).MicrosecondsPerQuarterNote));

            var notes = midiFile.GetNotes();
            var notesInLoop = notes.Where(n => n.Time < loopEndTimeTicks && n.EndTime > loopStartTimeTicks);

            foreach (var note in notesInLoop)
            {
                long newStartTicks = Math.Max(note.Time, loopStartTimeTicks);
                long newEndTicks = Math.Min(note.EndTime, loopEndTimeTicks);
                newStartTicks -= loopStartTimeTicks;
                newEndTicks -= loopStartTimeTicks;

                if (newEndTicks > newStartTicks)
                {
                    var newNote = new Note(note.NoteNumber, newEndTicks - newStartTicks, newStartTicks)
                    {
                        Velocity = note.Velocity,
                        OffVelocity = note.OffVelocity,
                        Channel = note.Channel
                    };
                    loopedTrackChunk.AddObjects(new[] { newNote });
                }
            }

            var loopedMidiFile = new MidiFile(loopedTrackChunk);
            // The new file also needs its time division set
            loopedMidiFile.TimeDivision = midiFile.TimeDivision;
            playback = loopedMidiFile.GetTimedEvents().Select(e => e.Event).GetPlayback(loopedMidiFile.GetTempoMap());
            playback.Loop = true;
        }
        else
        {
            var allEvents = midiFile.Chunks.OfType<TrackChunk>().SelectMany(c => c.Events);
            playback = allEvents.GetPlayback(midiFile.GetTempoMap());
        }

        _playback = playback;
        _playback.NotesPlaybackStarted += OnNotesPlaybackStarted;
        _playback.NotesPlaybackFinished += OnNotesPlaybackFinished;
        _playback.Start();
    }

    public void Stop()
    {
        if (_playback != null)
        {
            _playback.Stop();
            _playback.NotesPlaybackStarted -= OnNotesPlaybackStarted;
            _playback.NotesPlaybackFinished -= OnNotesPlaybackFinished;
            _playback.Dispose();
            _playback = null;
        }
        _audioEngine.StopAllVoices();
        _currentSong = null;
    }

    private void OnNotesPlaybackStarted(object? sender, NotesEventArgs e)
    {
        if (_currentSong is null) return;

        foreach (var note in e.Notes)
        {
            if (note.Channel < _currentSong.Tracks.Count)
            {
                var track = _currentSong.Tracks[note.Channel];
                _audioEngine.NoteOn(note.NoteNumber, note.Velocity, track.OscillatorType);
            }
            else
            {
                _audioEngine.NoteOn(note.NoteNumber, note.Velocity, OscillatorType.Sine);
            }
        }
    }

    private void OnNotesPlaybackFinished(object? sender, NotesEventArgs e)
    {
        foreach (var note in e.Notes)
        {
            _audioEngine.NoteOff(note.NoteNumber);
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
