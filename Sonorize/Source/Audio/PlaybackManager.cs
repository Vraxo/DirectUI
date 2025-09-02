using System;
using System.Collections.Generic;
using System.Linq;
using ManagedBass;

namespace Sonorize.Audio;

public enum PlaybackEndAction { NextInQueue, RepeatSong, DoNothing }
public enum PlaybackMode { Sequential, Shuffle }

public class PlaybackManager : IDisposable
{
    private readonly AudioPlayer _audioPlayer;
    private readonly Random _random = new();
    private IReadOnlyList<MusicFile> _tracklist = new List<MusicFile>();

    public int CurrentTrackIndex { get; private set; } = -1;
    public MusicFile? CurrentTrack => (CurrentTrackIndex >= 0 && CurrentTrackIndex < _tracklist.Count) ? _tracklist[CurrentTrackIndex] : null;

    public PlaybackState State => _audioPlayer.CurrentState;
    public bool IsPlaying => State == PlaybackState.Playing;
    public double CurrentPosition => _audioPlayer.GetPosition();
    public double TotalDuration => _audioPlayer.GetLength();
    public bool HasTracks => _tracklist.Any();

    public PlaybackEndAction EndAction { get; set; } = PlaybackEndAction.NextInQueue;
    public PlaybackMode Mode { get; set; } = PlaybackMode.Sequential;

    public PlaybackManager(AudioPlayer audioPlayer)
    {
        _audioPlayer = audioPlayer;
    }

    public void SetTracklist(IReadOnlyList<MusicFile> tracks)
    {
        _tracklist = tracks;
        // If the current track is no longer in the list, stop playback.
        if (CurrentTrackIndex >= _tracklist.Count)
        {
            Stop();
        }
    }

    public void Play(int trackIndex)
    {
        if (trackIndex < 0 || trackIndex >= _tracklist.Count)
        {
            Stop();
            return;
        }

        CurrentTrackIndex = trackIndex;
        var track = _tracklist[CurrentTrackIndex];
        _audioPlayer.Play(track.FilePath);
    }

    public void TogglePlayPause()
    {
        if (CurrentTrackIndex == -1 && _tracklist.Any())
        {
            Play(0); // Start from the first track if nothing is playing
        }
        else
        {
            switch (State)
            {
                case PlaybackState.Playing:
                    _audioPlayer.Pause();
                    break;
                case PlaybackState.Paused:
                    _audioPlayer.Resume();
                    break;
                case PlaybackState.Stopped:
                    if (CurrentTrack != null) Play(CurrentTrackIndex);
                    break;
            }
        }
    }

    public void Stop()
    {
        _audioPlayer.Stop();
        CurrentTrackIndex = -1;
    }

    public void Next()
    {
        if (!_tracklist.Any()) return;

        int nextIndex = GetNextTrackIndex();
        Play(nextIndex);
    }

    public void Previous()
    {
        if (!_tracklist.Any()) return;

        int prevIndex = CurrentTrackIndex - 1;
        if (prevIndex < 0)
        {
            prevIndex = _tracklist.Count - 1;
        }
        Play(prevIndex);
    }

    public void Seek(double position)
    {
        _audioPlayer.Seek(position);
    }

    public void Update()
    {
        // A song is considered "finished" if its state was playing but is now stopped.
        // The AudioPlayer doesn't emit an event, so we poll.
        // The state check `CurrentTrackIndex != -1` ensures we don't trigger this logic
        // for a player that was stopped manually.
        if (State == PlaybackState.Stopped && CurrentTrackIndex != -1)
        {
            HandleSongFinished();
        }
    }

    private void HandleSongFinished()
    {
        switch (EndAction)
        {
            case PlaybackEndAction.DoNothing:
                Stop();
                break;

            case PlaybackEndAction.RepeatSong:
                Play(CurrentTrackIndex); // Replay the same track
                break;

            case PlaybackEndAction.NextInQueue:
                Next();
                break;
        }
    }

    private int GetNextTrackIndex()
    {
        if (_tracklist.Count <= 1) return 0;

        if (Mode == PlaybackMode.Shuffle)
        {
            int next;
            do
            {
                next = _random.Next(_tracklist.Count);
            } while (next == CurrentTrackIndex); // Ensure it's a different track
            return next;
        }
        else // Sequential
        {
            int nextIndex = CurrentTrackIndex + 1;
            if (nextIndex >= _tracklist.Count)
            {
                nextIndex = 0; // Wrap around
            }
            return nextIndex;
        }
    }

    public void Dispose()
    {
        _audioPlayer.Dispose();
        GC.SuppressFinalize(this);
    }
}