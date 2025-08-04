using System.Numerics;
using Daw.Core;
using DirectUI;

namespace Daw.Views;

public class TrackListView
{
    public void Draw(Song song, ref int activeTrackIndex)
    {
        UI.Text("track_list_header", "Tracks");
        UI.Separator(100); // Use a dynamic width later

        for (int i = 0; i < song.Tracks.Count; i++)
        {
            var track = song.Tracks[i];
            bool isActive = (i == activeTrackIndex);

            var theme = new ButtonStylePack();
            theme.Normal.FillColor = isActive ? DawTheme.Accent : DawTheme.ControlFill;
            theme.Hover.FillColor = isActive ? DawTheme.AccentBright : DawTheme.ControlFillHover;
            theme.Pressed.FillColor = DawTheme.AccentBright;

            if (UI.Button($"track_{i}", track.Name, new Vector2(120, 30), theme, isActive: isActive))
            {
                activeTrackIndex = i;
            }
        }

        if (UI.Button("add_track_button", "+ Add Track", new Vector2(120, 25), DawTheme.ToolbarButton))
        {
            song.Tracks.Add(new MidiTrack($"Track {song.Tracks.Count + 1}"));
            // Automatically select the new track
            activeTrackIndex = song.Tracks.Count - 1;
        }
    }
}
