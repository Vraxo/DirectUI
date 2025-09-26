using System;
using Daw.Core;
using DirectUI;

namespace Daw.Views;

/// <summary>
/// A view that displays and allows editing of properties for the currently selected track.
/// </summary>
public class InspectorView
{
    public void Draw(MidiTrack? activeTrack)
    {
        UI.Text("inspector_header", "Inspector");
        UI.Separator(200);

        if (activeTrack is null)
        {
            UI.Text("inspector_no_track", "No track selected.");
            return;
        }

        // Display Track Name
        UI.Text("inspector_track_name_label", "Track Name", style: new ButtonStyle { FontColor = DawTheme.TextDim });
        UI.Text("inspector_track_name", activeTrack.Name, style: new ButtonStyle { FontSize = 16 });

        UI.Separator(200, verticalPadding: 10);

        // Instrument (Oscillator) Selection
        UI.Text("inspector_instrument_label", "Instrument", style: new ButtonStyle { FontColor = DawTheme.TextDim });
        
        int selectedIndex = (int)activeTrack.OscillatorType;
        string[] oscillatorNames = Enum.GetNames(typeof(OscillatorType));
        if (UI.Combobox($"inspector_osc_combo", ref selectedIndex, oscillatorNames, new(220, 25)))
        {
            activeTrack.OscillatorType = (OscillatorType)selectedIndex;
        }

        // Future controls like Volume, Pan, Mute, Solo would go here
    }
}
