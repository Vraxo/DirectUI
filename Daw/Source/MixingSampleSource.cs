using System;
using System.Collections.Generic;
using System.Linq;
using CSCore;

namespace Daw.Audio;

/// <summary>
/// A custom sample source that mixes multiple input sources by summing their samples.
/// This replaces the need for a specific mixing class from the CSCore library.
/// </summary>
public class MixingSampleSource : ISampleSource
{
    private readonly List<ISampleSource> _sampleSources = new();
    private readonly object _lockObject = new();
    private float[]? _sourceBuffer;

    public WaveFormat WaveFormat { get; }
    public long Position { get; set; }
    public long Length => 0; // Infinite
    public bool CanSeek => false;

    public MixingSampleSource(int channels, int sampleRate)
    {
        WaveFormat = new WaveFormat(sampleRate, 32, channels, AudioEncoding.IeeeFloat);
    }

    public void AddSource(ISampleSource source)
    {
        lock (_lockObject)
        {
            if (!_sampleSources.Contains(source))
            {
                _sampleSources.Add(source);
            }
        }
    }

    public void RemoveSource(ISampleSource source)
    {
        lock (_lockObject)
        {
            _sampleSources.Remove(source);
        }
    }

    public IReadOnlyList<ISampleSource> GetSources()
    {
        lock (_lockObject)
        {
            return _sampleSources.ToList();
        }
    }


    public int Read(float[] buffer, int offset, int count)
    {
        // Ensure our temporary buffer for reading from sources is large enough
        if (_sourceBuffer is null || _sourceBuffer.Length < count)
        {
            _sourceBuffer = new float[count];
        }

        // Clear the main output buffer first
        Array.Clear(buffer, offset, count);

        // Create a thread-safe copy of the sources to iterate over
        ISampleSource[] sourcesToProcess;
        lock (_lockObject)
        {
            sourcesToProcess = _sampleSources.ToArray();
        }

        // Read from each source and sum its output into the main buffer
        foreach (var source in sourcesToProcess)
        {
            int read = source.Read(_sourceBuffer, 0, count);
            for (int i = 0; i < read; i++)
            {
                buffer[offset + i] += _sourceBuffer[i];
            }
        }

        // We are not doing any clipping here. If the sum of sources
        // exceeds 1.0f, it will clip. For this simple synth, that is acceptable.
        return count;
    }

    public void Dispose()
    {
        // This mixer does not own the sources, so it does not dispose them.
    }
}