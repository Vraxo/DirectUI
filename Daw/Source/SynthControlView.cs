// Daw/Source/Views/SynthControlView.cs
using System.Numerics;
using Daw.Audio;
using DirectUI;
using Vortice.Mathematics;

namespace Daw.Views;

public class SynthControlView
{
    public void Draw(Rect viewArea, SynthParameters parameters)
    {
        UI.Context.Renderer.DrawBox(viewArea, new BoxStyle { FillColor = DawTheme.PanelBackground, Roundness = 0 });

        UI.BeginVBoxContainer("synth_controls", viewArea.TopLeft + new Vector2(10, 10), 10);

        UI.Text("synth_header", "Synthesizer Controls");

        UI.Separator(viewArea.Width - 20);

        // Oscillator Type
        var oscTypes = System.Enum.GetNames<OscillatorType>();
        int selectedOsc = (int)parameters.OscillatorType;
        if (UI.Combobox("osc_type", ref selectedOsc, oscTypes, new Vector2(120, 25)))
        {
            parameters.OscillatorType = (OscillatorType)selectedOsc;
        }

        // Filter Cutoff
        UI.Text("filter_cutoff_label", $"Cutoff: {parameters.FilterCutoff:F0} Hz");
        parameters.FilterCutoff = UI.HSlider("filter_cutoff", parameters.FilterCutoff, 20f, 20000f, new Vector2(120, 16));

        // Filter Resonance
        UI.Text("filter_res_label", $"Resonance: {parameters.FilterResonance:F2}");
        parameters.FilterResonance = UI.HSlider("filter_res", parameters.FilterResonance, 0.707f, 5f, new Vector2(120, 16));

        UI.Separator(viewArea.Width - 20);

        // ADSR Controls
        UI.Text("adsr_header", "Volume Envelope (ADSR)");
        UI.Text("adsr_attack_label", $"Attack: {parameters.AmpEnvAttackTime * 1000:F0} ms");
        parameters.AmpEnvAttackTime = UI.HSlider("adsr_attack", parameters.AmpEnvAttackTime, 0.001f, 2f, new Vector2(120, 16));

        UI.Text("adsr_decay_label", $"Decay: {parameters.AmpEnvDecayTime * 1000:F0} ms");
        parameters.AmpEnvDecayTime = UI.HSlider("adsr_decay", parameters.AmpEnvDecayTime, 0.001f, 2f, new Vector2(120, 16));

        UI.Text("adsr_sustain_label", $"Sustain: {parameters.AmpEnvSustainLevel:P0}");
        parameters.AmpEnvSustainLevel = UI.HSlider("adsr_sustain", parameters.AmpEnvSustainLevel, 0.0f, 1f, new Vector2(120, 16));

        UI.Text("adsr_release_label", $"Release: {parameters.AmpEnvReleaseTime * 1000:F0} ms");
        parameters.AmpEnvReleaseTime = UI.HSlider("adsr_release", parameters.AmpEnvReleaseTime, 0.001f, 5f, new Vector2(120, 16));

        UI.EndVBoxContainer();
    }
}