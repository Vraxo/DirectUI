using System;
using System.Collections.Generic;
using System.Linq;
using CSCore;
using CSCore.CoreAudioAPI;
using CSCore.SoundOut;

namespace Daw.Audio;

/// <summary>
/// Manages audio output and hosts the synthesizer voices.
/// </summary>
public class AudioEngine : IDisposable
{
    private readonly ISoundOut _soundOut;
    private readonly MixingSampleSource _mixer;
    private readonly List<SynthesizerVoice> _voices = new();
    private readonly SynthParameters _synthParameters;
    private const int MaxVoices = 32; // Limit polyphony

    public AudioEngine(SynthParameters synthParameters)
    {
        _synthParameters = synthParameters ?? throw new ArgumentNullException(nameof(synthParameters));

        // Use WasapiOut in exclusive mode with a low latency buffer (20ms).
        _soundOut = new WasapiOut(true, AudioClientShareMode.Exclusive, 20);
        _mixer = new MixingSampleSource(1, 44100);

        // Create a pool of synthesizer voices to be reused.
        for (int i = 0; i < MaxVoices; i++)
        {
            _voices.Add(new SynthesizerVoice(44100));
        }

        _soundOut.Initialize(_mixer.ToWaveSource());
        _soundOut.Play();
    }

    public void NoteOn(int pitch, int velocity)
    {
        // Find a free (inactive) voice to play the note.
        var voice = _voices.FirstOrDefault(v => !v.IsActive);
        if (voice != null)
        {
            // Configure the voice with the current synthesizer parameters
            voice.Configure(_synthParameters, pitch, velocity);
            _mixer.AddSource(voice);
        }
        else
        {
            Console.WriteLine("Warning: No free voices available (polyphony limit reached).");
        }
    }

    public void NoteOff(int pitch)
    {
        // Find all voices currently playing this pitch and trigger their release phase.
        var freq = (float)SynthesizerVoice.MidiToFrequency(pitch);
        foreach (var voice in _voices.Where(v => v.IsActive && Math.Abs(v.Frequency - freq) < 0.1))
        {
            voice.NoteOff();
        }
    }

    public void StopAllVoices()
    {
        foreach (var voice in _voices.Where(v => v.IsActive))
        {
            voice.NoteOff();
        }
    }

    public void Update()
    {
        // Find voices that are part of the mixer but are no longer active
        var inactiveVoices = _mixer.GetSources()
                                   .OfType<SynthesizerVoice>()
                                   .Where(v => !v.IsActive)
                                   .ToList();

        // Remove them from the mixer so they are no longer processed
        foreach (var voice in inactiveVoices)
        {
            _mixer.RemoveSource(voice);
        }
    }

    public void Dispose()
    {
        _soundOut?.Stop();
        _soundOut?.Dispose();
    }
}