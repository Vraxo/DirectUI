// Daw/Source/Filter.cs
using System;

namespace Daw.Audio;

/// <summary>
/// A simple state-variable low-pass filter.
/// This is a common and good-sounding digital filter implementation.
/// </summary>
public class Filter
{
    private readonly int _sampleRate;
    private float _low;  // Low-pass output
    private float _band; // Band-pass output

    public Filter(int sampleRate)
    {
        _sampleRate = sampleRate;
    }

    /// <summary>
    /// Processes a single input sample and returns the filtered output.
    /// </summary>
    /// <param name="input">The raw audio sample to be filtered.</param>
    /// <param name="cutoff">The filter's cutoff frequency in Hz.</param>
    /// <param name="resonance">The resonance (Q factor), typically from 0.707 to ~10.</param>
    /// <returns>The filtered audio sample.</returns>
    public float Process(float input, float cutoff, float resonance)
    {
        // Clamp cutoff to prevent issues with Nyquist frequency
        cutoff = Math.Clamp(cutoff, 20f, _sampleRate / 2.1f);
        resonance = Math.Max(0.0f, resonance);

        // This is the core of the state-variable filter calculation
        float f = 2.0f * (float)Math.Sin(Math.PI * cutoff / _sampleRate);
        float q = 1.0f / resonance;

        // Process the filter stages
        _low += f * _band;
        float high = input - _low - q * _band;
        _band += f * high;

        // Apply a soft-clipping function to prevent the filter from exploding at high resonance
        _low = MathF.Tanh(_low);

        return _low; // Return the low-pass filtered signal
    }
}