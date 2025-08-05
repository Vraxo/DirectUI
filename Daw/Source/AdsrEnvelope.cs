using System;

namespace Daw.Audio;

/// <summary>
/// A simple, linear ADSR (Attack, Decay, Sustain, Release) envelope generator.
/// </summary>
public class AdsrEnvelope
{
    public enum AdsrState { Idle, Attack, Decay, Sustain, Release }

    private readonly int _sampleRate;
    private float _attackRate;
    private float _decayRate;
    private float _releaseRate;

    public AdsrState State { get; private set; } = AdsrState.Idle;
    public float Output { get; private set; }

    public float AttackTime { get; set; } = 0.01f;
    public float DecayTime { get; set; } = 0.1f;
    public float SustainLevel { get; set; } = 0.7f;
    public float ReleaseTime { get; set; } = 0.2f;

    public AdsrEnvelope(int sampleRate)
    {
        _sampleRate = Math.Max(1, sampleRate);
        UpdateRates();
    }

    private void UpdateRates()
    {
        _attackRate = AttackTime > 0 ? 1.0f / (AttackTime * _sampleRate) : 1.0f;
        _decayRate = DecayTime > 0 ? (1.0f - SustainLevel) / (DecayTime * _sampleRate) : 1.0f;
        _releaseRate = ReleaseTime > 0 ? SustainLevel / (ReleaseTime * _sampleRate) : 1.0f;
    }

    public void NoteOn()
    {
        UpdateRates(); // Recalculate in case properties changed
        State = AdsrState.Attack;
        Output = 0.0f;
    }

    public void NoteOff()
    {
        State = AdsrState.Release;
    }

    public float Process()
    {
        switch (State)
        {
            case AdsrState.Idle:
                Output = 0.0f;
                break;
            case AdsrState.Attack:
                Output += _attackRate;
                if (Output >= 1.0f)
                {
                    Output = 1.0f;
                    State = AdsrState.Decay;
                }
                break;
            case AdsrState.Decay:
                Output -= _decayRate;
                if (Output <= SustainLevel)
                {
                    Output = SustainLevel;
                    State = AdsrState.Sustain;
                }
                break;
            case AdsrState.Sustain:
                Output = SustainLevel;
                break;
            case AdsrState.Release:
                Output -= _releaseRate;
                if (Output <= 0.0f)
                {
                    Output = 0.0f;
                    State = AdsrState.Idle;
                }
                break;
        }
        return Output;
    }
}