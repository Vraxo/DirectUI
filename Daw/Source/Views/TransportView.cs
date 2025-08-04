using System.Numerics;
using Daw.Audio;
using Daw.Core;
using DirectUI;

namespace Daw.Views;

public class TransportView
{
    private readonly MidiEngine _midiEngine;
    private string _tempoString = "120.0";

    public TransportView(MidiEngine midiEngine)
    {
        _midiEngine = midiEngine;
    }

    public void Draw(Vector2 position, Song song)
    {
        UI.BeginHBoxContainer("transport_controls", position, 10);

        // Play Button
        if (UI.Button("play_button", "▶", new Vector2(30, 30), theme: DawTheme.TransportButton))
        {
            _midiEngine.Play(song);
        }

        // Stop Button
        if (UI.Button("stop_button", "■", new Vector2(30, 30), theme: DawTheme.TransportButton))
        {
            _midiEngine.Stop();
        }

        // Tempo Display and Input
        UI.BeginVBoxContainer("tempo_vbox", UI.Context.Layout.GetCurrentPosition(), 0);
        UI.Text("tempo_label", "BPM", style: new ButtonStyle { FontColor = DawTheme.TextDim, FontSize = 10 });
        if (UI.State.FocusedElementId != "tempo_input".GetHashCode())
        {
            _tempoString = song.Tempo.ToString("F1");
        }
        if (UI.InputText("tempo_input", ref _tempoString, new Vector2(50, 20)))
        {
            if (double.TryParse(_tempoString, out double newTempo))
            {
                song.Tempo = newTempo;
            }
        }
        UI.EndVBoxContainer();

        UI.EndHBoxContainer();
    }
}