using System;
using ManagedBass;

namespace Sonorize.Audio;

public class AudioPlayer : IDisposable
{
    private int _stream;
    private bool _isDisposed;

    public bool IsPlaying => Bass.ChannelIsActive(_stream) == PlaybackState.Playing;

    public AudioPlayer()
    {
        if (!Bass.Init())
            throw new Exception("Failed to initialize audio device.");
    }

    /// <summary>
    /// Loads an audio file (M4A, MP3, WAV, etc.) and starts playback.
    /// </summary>
    public void Play(string filePath)
    {
        if (_isDisposed) throw new ObjectDisposedException(nameof(AudioPlayer));
        if (!System.IO.File.Exists(filePath)) throw new ArgumentException("File does not exist", nameof(filePath));

        StopInternal();

        _stream = Bass.CreateStream(filePath, 0, 0, BassFlags.Default);
        if (_stream == 0)
            throw new Exception($"Failed to create audio stream: {Bass.LastError}");

        Bass.ChannelPlay(_stream);
    }

    public void Pause()
    {
        if (_isDisposed) throw new ObjectDisposedException(nameof(AudioPlayer));
        if (_stream != 0)
            Bass.ChannelPause(_stream);
    }

    public void Resume()
    {
        if (_isDisposed) throw new ObjectDisposedException(nameof(AudioPlayer));
        if (_stream != 0)
            Bass.ChannelPlay(_stream);
    }

    public void Stop()
    {
        if (_isDisposed) throw new ObjectDisposedException(nameof(AudioPlayer));
        StopInternal();
    }

    /// <summary>
    /// Seeks to the specified position in seconds.
    /// </summary>
    public void Seek(double seconds)
    {
        if (_isDisposed) throw new ObjectDisposedException(nameof(AudioPlayer));
        if (_stream != 0)
        {
            long pos = Bass.ChannelSeconds2Bytes(_stream, seconds);
            Bass.ChannelSetPosition(_stream, pos);
        }
    }

    /// <summary>
    /// Gets the current playback position in seconds.
    /// </summary>
    public double GetPosition()
    {
        if (_isDisposed) throw new ObjectDisposedException(nameof(AudioPlayer));
        return _stream != 0 ? Bass.ChannelBytes2Seconds(_stream, Bass.ChannelGetPosition(_stream)) : 0;
    }

    /// <summary>
    /// Gets the total length of the current stream in seconds.
    /// </summary>
    public double GetLength()
    {
        if (_isDisposed) throw new ObjectDisposedException(nameof(AudioPlayer));
        return _stream != 0 ? Bass.ChannelBytes2Seconds(_stream, Bass.ChannelGetLength(_stream)) : 0;
    }

    private void StopInternal()
    {
        if (_stream != 0)
        {
            Bass.ChannelStop(_stream);
            Bass.StreamFree(_stream);
            _stream = 0;
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;

        StopInternal();
        Bass.Free();
        _isDisposed = true;
        GC.SuppressFinalize(this);
    }

    ~AudioPlayer()
    {
        Dispose();
    }
}