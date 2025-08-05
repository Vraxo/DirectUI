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
    private const int MaxVoices = 32; // Limit polyphony

    public AudioEngine()
    {
        // Use WasapiOut in exclusive mode with a low latency buffer (20ms).
        // This is the best practice for a DAW to minimize the delay between a note event and the audible sound.
        _soundOut = new WasapiOut(true, AudioClientShareMode.Exclusive, 20);

        // Use our new custom mixer.
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
            // The mixer needs to be aware of the voice to process its audio.
            _mixer.AddSource(voice);
            voice.NoteOn(pitch, velocity);
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

    /// <summary>
    /// Tells all currently active voices to start their release phase.
    /// This is thread-safe and can be called from any thread.
    /// </summary>
    public void StopAllVoices()
    {
        foreach (var voice in _voices.Where(v => v.IsActive))
        {
            voice.NoteOff();
        }
    }


    /// <summary>
    /// Called periodically to clean up voices that have finished their release phase.
    /// </summary>
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