using CSCore;
using CSCore.Streams;
using System;

namespace Daw.Audio;

/// <summary>
/// Represents a single note being played by the synthesizer.
/// Implements ISampleSource to be used by CSCore's audio pipeline.
/// </summary>
public class SynthesizerVoice : ISampleSource
{
    private readonly SineGenerator _sineGenerator;
    private readonly AdsrEnvelope _adsr;

    public WaveFormat WaveFormat { get; }
    public long Position { get; set; }

    // SineGenerator is an infinite source, so it has no defined length.
    public long Length => 0;

    // A synthesizer generates a continuous stream, so it cannot be seeked.
    public bool CanSeek => false;

    // A public property to read the frequency for NoteOff matching.
    public float Frequency => (float)_sineGenerator.Frequency;

    public SynthesizerVoice(int sampleRate)
    {
        // FIX: The WaveFormat must match the mixer's format, which is 32-bit float.
        WaveFormat = new WaveFormat(sampleRate, 32, 1, AudioEncoding.IeeeFloat);

        _sineGenerator = new SineGenerator(sampleRate, 0, 0) { Amplitude = 0.2f };
        _adsr = new AdsrEnvelope(sampleRate)
        {
            AttackTime = 0.01f,
            DecayTime = 0.1f,
            SustainLevel = 0.7f,
            ReleaseTime = 0.2f
        };
    }

    public void NoteOn(int pitch, int velocity)
    {
        _sineGenerator.Frequency = (float)MidiToFrequency(pitch);
        _sineGenerator.Amplitude = (velocity / 127f) * 0.2f; // Velocity affects amplitude
        _adsr.NoteOn();
    }

    public void NoteOff()
    {
        _adsr.NoteOff();
    }

    /// <summary>
    /// Returns true if the voice is currently active (i.e., not in the idle state of its envelope).
    /// </summary>
    public bool IsActive => _adsr.State != AdsrEnvelope.AdsrState.Idle;

    public int Read(float[] buffer, int offset, int count)
    {
        // SineGenerator is infinite, so it will always fill the buffer.
        int samplesRead = count;
        _sineGenerator.Read(buffer, offset, count);

        // Apply the ADSR envelope to each sample
        for (int i = 0; i < samplesRead; i++)
        {
            buffer[offset + i] *= _adsr.Process();
        }

        return samplesRead;
    }

    /// <summary>
    /// Converts a MIDI note number (pitch) to its corresponding frequency in Hz.
    /// </summary>
    public static double MidiToFrequency(int midiNote)
    {
        return 440.0 * Math.Pow(2.0, (midiNote - 69.0) / 12.0);
    }

    public void Dispose()
    {
        // This class doesn't own unmanaged resources directly.
    }
}