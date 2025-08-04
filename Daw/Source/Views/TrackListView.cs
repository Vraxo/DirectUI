using System;
using System.Numerics;
using Daw.Core;
using DirectUI;

namespace Daw.Views;

public class TrackListView
{
    public enum TrackAction { None, Rename, Delete, MoveUp, MoveDown }
    private (TrackAction, int) _requestedAction = (TrackAction.None, -1);

    // State to remember which track's context menu is active across frames
    private int _contextMenuTrackIndex = -1;

    public void Draw(Song song, ref int activeTrackIndex)
    {
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

            // --- Open Context Menu on Right-Click ---
            if (UI.BeginContextMenu(trackId))
            {
                // Remember which track's menu we need to draw and check for results
                _contextMenuTrackIndex = i;
                Console.WriteLine($"[DEBUG] Opening context menu for track index: {i}");
            }
        }

        // --- Draw the Active Context Menu (if any) ---
        // This is now outside the loop, so we check for the result every frame.
        if (_contextMenuTrackIndex != -1)
        {
            // The context menu system is modal, so only one can be open.
            // We use the index to generate a unique ID.
            int choice = UI.ContextMenu($"track_context_{_contextMenuTrackIndex}", new[] { "Rename", "Delete", "Move Up", "Move Down" });

            if (choice != -1)
            {
                Console.WriteLine($"[DEBUG] ContextMenu returned choice: {choice} for track index: {_contextMenuTrackIndex}");
                switch (choice)
                {
                    case 0: _requestedAction = (TrackAction.Rename, _contextMenuTrackIndex); break;
                    case 1: _requestedAction = (TrackAction.Delete, _contextMenuTrackIndex); break;
                    case 2: _requestedAction = (TrackAction.MoveUp, _contextMenuTrackIndex); break;
                    case 3: _requestedAction = (TrackAction.MoveDown, _contextMenuTrackIndex); break;
                }
                Console.WriteLine($"[DEBUG] RequestedAction set to: {_requestedAction.Item1}");

                // We got a result, so stop showing the menu.
                _contextMenuTrackIndex = -1;
            }
            // If the popup was closed by clicking away, UI.State.IsPopupOpen will become false.
            // We should stop trying to draw the menu in that case.
            else if (!UI.State.IsPopupOpen)
            {
                _contextMenuTrackIndex = -1;
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
    /// This "consumes" the action, resetting it to None after being read.
    /// </summary>
    public (TrackAction action, int trackIndex) GetAction()
    {
        var actionToReturn = _requestedAction;
        if (actionToReturn.Item1 != TrackAction.None)
        {
            Console.WriteLine($"[DEBUG] DawAppLogic is consuming action: {actionToReturn.Item1} for index {actionToReturn.Item2}");
            // Reset the action immediately after it's been read to ensure it only fires once.
            _requestedAction = (TrackAction.None, -1);
        }
        return actionToReturn;
    }
}