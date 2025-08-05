using CSCore;
using CSCore.Streams;
using System;
using Daw.Core;

namespace Daw.Audio;

/// <summary>
/// Represents a single note being played by the synthesizer.
/// Implements ISampleSource to be used by CSCore's audio pipeline.
/// </summary>
public class SynthesizerVoice : ISampleSource
{
    private readonly AdsrEnvelope _adsr;
    private double _phase;
    private double _amplitude;
    private double _frequency;

    public OscillatorType OscillatorType { get; set; } = OscillatorType.Sine;
    public WaveFormat WaveFormat { get; }
    public long Position { get; set; }
    public long Length => 0; // Infinite
    public bool CanSeek => false;
    public float Frequency => (float)_frequency;

    public SynthesizerVoice(int sampleRate)
    {
        WaveFormat = new WaveFormat(sampleRate, 32, 1, AudioEncoding.IeeeFloat);
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
        _frequency = MidiToFrequency(pitch);
        _amplitude = (velocity / 127f) * 0.2f;
        _phase = 0; // Reset phase on new note
        _adsr.NoteOn();
    }

    public void NoteOff()
    {
        _adsr.NoteOff();
    }

    public bool IsActive => _adsr.State != AdsrEnvelope.AdsrState.Idle;

    public int Read(float[] buffer, int offset, int count)
    {
        double phaseIncrement = 2.0 * Math.PI * _frequency / WaveFormat.SampleRate;

        for (int i = 0; i < count; i++)
        {
            float sample;
            switch (OscillatorType)
            {
                case OscillatorType.Square:
                    sample = _phase < Math.PI ? 1.0f : -1.0f;
                    break;
                case OscillatorType.Sawtooth:
                    sample = (float)(_phase / Math.PI - 1.0); // Ramps from -1 to 1
                    break;
                case OscillatorType.Triangle:
                    sample = (float)(2.0 / Math.PI * Math.Asin(Math.Sin(_phase)));
                    break;
                case OscillatorType.Sine:
                default:
                    sample = (float)Math.Sin(_phase);
                    break;
            }

            buffer[offset + i] = sample * (float)_amplitude * _adsr.Process();

            _phase += phaseIncrement;
            if (_phase >= 2.0 * Math.PI)
            {
                _phase -= 2.0 * Math.PI;
            }
        }
        return count;
    }

    public static double MidiToFrequency(int midiNote)
    {
        return 440.0 * Math.Pow(2.0, (midiNote - 69.0) / 12.0);
    }

    public void Dispose()
    {
        // This class doesn't own unmanaged resources directly.
    }
}
