// Daw/Source/SynthParameters.cs
namespace Daw.Audio;

/// <summary>
/// Encapsulates the settings for a synthesizer voice.
/// This allows the UI to modify a single state object that the audio engine can read from.
/// </summary>
public class SynthParameters
{
    public OscillatorType OscillatorType { get; set; } = OscillatorType.Sawtooth;

    // Filter Settings
    public float FilterCutoff { get; set; } = 20000f; // Default: No filtering
    public float FilterResonance { get; set; } = 0.707f; // Default: No resonance

    // Amplitude Envelope Settings
    public float AmpEnvAttackTime { get; set; } = 0.01f;
    public float AmpEnvDecayTime { get; set; } = 0.1f;
    public float AmpEnvSustainLevel { get; set; } = 0.8f;
    public float AmpEnvReleaseTime { get; set; } = 0.2f;
}