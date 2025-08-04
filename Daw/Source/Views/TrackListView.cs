using System.Numerics;
using Daw.Core;
using DirectUI;

namespace Daw.Views;

public class TrackListView
{
    public enum TrackAction { None, Rename, Delete, MoveUp, MoveDown }
    private (TrackAction, int) _requestedAction = (TrackAction.None, -1);
    
    public void Draw(Song song, ref int activeTrackIndex)
    {
        // Reset action at the start of the frame
        _requestedAction = (TrackAction.None, -1);

        UI.Text("track_list_header", "Tracks");
        UI.Separator(100); // Use a dynamic width later

        for (int i = 0; i < song.Tracks.Count; i++)
        {
            var track = song.Tracks[i];
            bool isActive = (i == activeTrackIndex);
            string trackId = $"track_{i}";

            // --- Draw the main track button ---
            var theme = new ButtonStylePack();
            theme.Normal.FillColor = isActive ? DawTheme.Accent : DawTheme.ControlFill;
            theme.Hover.FillColor = isActive ? DawTheme.AccentBright : DawTheme.ControlFillHover;
            theme.Pressed.FillColor = DawTheme.AccentBright;

            if (UI.Button(trackId, track.Name, new Vector2(120, 30), theme, isActive: isActive, clickBehavior: DirectUI.Button.ClickBehavior.Left))
            {
                activeTrackIndex = i;
            }

            // --- Handle Context Menu ---
            if (UI.BeginContextMenu(trackId))
            {
                int choice = UI.ContextMenu($"track_context_{i}", new[] { "Rename", "Delete", "Move Up", "Move Down" });
                switch (choice)
                {
                    case 0: _requestedAction = (TrackAction.Rename, i); break;
                    case 1: _requestedAction = (TrackAction.Delete, i); break;
                    case 2: _requestedAction = (TrackAction.MoveUp, i); break;
                    case 3: _requestedAction = (TrackAction.MoveDown, i); break;
                }
            }
        }

        if (UI.Button("add_track_button", "+ Add Track", new Vector2(120, 25), DawTheme.ToolbarButton))
        {
            song.Tracks.Add(new MidiTrack($"Track {song.Tracks.Count + 1}"));
            // Automatically select the new track
            activeTrackIndex = song.Tracks.Count - 1;
        }
    }

    /// <summary>
    /// Allows the main app logic to poll for a requested action for a specific track.
    /// </summary>
    public (TrackAction action, int trackIndex) GetAction()
    {
        return _requestedAction;
    }
}
