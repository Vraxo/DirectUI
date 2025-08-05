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
    private readonly int _sampleRate;
    private readonly AdsrEnvelope _ampEnvelope;
    private readonly Filter _filter;

    private OscillatorType _oscillatorType;
    private double _phase;
    private float _amplitude;

    public WaveFormat WaveFormat { get; }
    public long Position { get; set; }
    public long Length => 0; // Infinite
    public bool CanSeek => false;

    public float Frequency { get; private set; }

    public SynthesizerVoice(int sampleRate)
    {
        _sampleRate = sampleRate;
        WaveFormat = new WaveFormat(sampleRate, 32, 1, AudioEncoding.IeeeFloat);

        _ampEnvelope = new AdsrEnvelope(sampleRate);
        _filter = new Filter(sampleRate);
    }

    public void Configure(SynthParameters parameters, int pitch, int velocity)
    {
        _oscillatorType = parameters.OscillatorType;

        // Configure amp envelope from parameters
        _ampEnvelope.AttackTime = parameters.AmpEnvAttackTime;
        _ampEnvelope.DecayTime = parameters.AmpEnvDecayTime;
        _ampEnvelope.SustainLevel = parameters.AmpEnvSustainLevel;
        _ampEnvelope.ReleaseTime = parameters.AmpEnvReleaseTime;

        // Configure voice properties from note event
        Frequency = (float)MidiToFrequency(pitch);
        _amplitude = (velocity / 127f) * 0.5f; // Reduce gain to prevent clipping with rich waveforms
        _phase = 0.0; // Reset phase for new note

        // Trigger the envelope
        _ampEnvelope.NoteOn();
    }

    public void NoteOff()
    {
        _ampEnvelope.NoteOff();
    }

    public bool IsActive => _ampEnvelope.State != AdsrEnvelope.AdsrState.Idle;

    public int Read(float[] buffer, int offset, int count)
    {
        // This is the core audio processing loop for the voice
        for (int i = 0; i < count; i++)
        {
            // 1. Generate Raw Oscillator Waveform
            float rawSample = GenerateSample();

            // 2. Apply Filter (currently static, will be modulated later)
            // For now, we use the parameters from the time the note was struck.
            // A more advanced synth would have its own envelopes for the filter.
            float filteredSample = _filter.Process(rawSample, 20000f, 0.707f); // Placeholder - real values should come from params

            // 3. Apply Amplitude Envelope (Volume)
            float envelopedSample = filteredSample * _ampEnvelope.Process();

            buffer[offset + i] = envelopedSample;
        }

        return count;
    }

    private float GenerateSample()
    {
        double increment = Frequency / _sampleRate;
        _phase += increment;
        if (_phase > 1.0) _phase -= 1.0;

        float value = 0.0f;
        switch (_oscillatorType)
        {
            case OscillatorType.Sine:
                value = (float)Math.Sin(_phase * 2.0 * Math.PI);
                break;
            case OscillatorType.Square:
                value = _phase < 0.5 ? 1.0f : -1.0f;
                break;
            case OscillatorType.Sawtooth:
                value = (float)(2.0 * _phase - 1.0);
                break;
            case OscillatorType.Triangle:
                value = (float)(2.0 * (0.5 - Math.Abs(_phase - 0.5)) * 2.0 - 1.0);
                break;
        }

        return value * _amplitude;
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