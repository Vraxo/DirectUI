using System;
using System.Numerics;
using Daw.Audio;
using Daw.Core;
using Daw.Views;
using DirectUI;
using DirectUI.Core;
using Vortice.Mathematics;

namespace Daw;

public class DawAppLogic : IAppLogic
{
    private readonly MidiEngine _midiEngine;
    private Song _song;
    private const string SongFilePath = "mysong.dawjson";

    // --- UI State ---
    private int _activeTrackIndex = 0;
    private float _leftPanelWidth = 150;
    private PianoRollTool _currentTool = PianoRollTool.Select;

    // --- Child Views ---
    private readonly MenuBarView _menuBarView;
    private readonly TransportView _transportView;
    private readonly TimelineView _timelineView;
    private readonly TrackListView _trackListView;
    private readonly PianoRollToolbarView _pianoRollToolbarView;
    private readonly PianoRollView _pianoRollView;
    private readonly IWindowHost _host;

    public DawAppLogic(IWindowHost host)
    {
        _host = host;
        _midiEngine = new MidiEngine();
        _song = SongSerializer.Load(SongFilePath) ?? CreateDefaultSong();

        _menuBarView = new MenuBarView();
        _transportView = new TransportView(_midiEngine);
        _timelineView = new TimelineView();
        _trackListView = new TrackListView();
        _pianoRollToolbarView = new PianoRollToolbarView();
        _pianoRollView = new PianoRollView();
    }

    private static Song CreateDefaultSong()
    {
        var song = new Song { Tempo = 120 };
        var leadTrack = new MidiTrack("Lead Melody");
        song.Tracks.Add(leadTrack);

        // Basic C Major Scale
        leadTrack.Events.Add(new NoteEvent(0, 480, 60, 100));     // C
        leadTrack.Events.Add(new NoteEvent(500, 480, 62, 100));    // D
        leadTrack.Events.Add(new NoteEvent(1000, 480, 64, 100));   // E
        leadTrack.Events.Add(new NoteEvent(1500, 480, 65, 100));   // F
        leadTrack.Events.Add(new NoteEvent(2000, 480, 67, 100));   // G
        leadTrack.Events.Add(new NoteEvent(2500, 480, 69, 100));   // A
        leadTrack.Events.Add(new NoteEvent(3000, 480, 71, 100));   // B
        leadTrack.Events.Add(new NoteEvent(3500, 480, 72, 100));   // C
        return song;
    }

    public void DrawUI(UIContext context)
    {
        // Poll the engine every frame for time-sensitive updates like looping
        _midiEngine.Update();
        
        var windowSize = context.Renderer.RenderTargetSize;

        // --- Global Actions ---
        var fileAction = _menuBarView.GetAction();
        switch (fileAction)
        {
            case MenuBarView.FileAction.Save:
                SongSerializer.Save(_song, SongFilePath);
                break;
            case MenuBarView.FileAction.Load:
                _midiEngine.Stop();
                _song = SongSerializer.Load(SongFilePath) ?? CreateDefaultSong();
                _activeTrackIndex = 0;
                break;
            case MenuBarView.FileAction.Export:
                _midiEngine.ExportToMidiFile(_song, "export.mid");
                break;
        }
        
        // --- Track Management Actions from Context Menu ---
        HandleTrackActions();

        // Ensure active track index is always valid
        if (_activeTrackIndex >= _song.Tracks.Count)
        {
            _activeTrackIndex = Math.Max(0, _song.Tracks.Count - 1);
        }

        // --- Draw UI Layout ---
        DrawTopBar(windowSize);
        DrawMainContent(windowSize);
    }

    private void HandleTrackActions()
    {
        var (action, index) = _trackListView.GetAction();
        if (action == TrackListView.TrackAction.None) return;

        switch (action)
        {
            case TrackListView.TrackAction.Delete:
                if (index >= 0 && index < _song.Tracks.Count)
                {
                    _song.Tracks.RemoveAt(index);
                }
                break;
            case TrackListView.TrackAction.Rename:
                if (index >= 0 && index < _song.Tracks.Count)
                {
                    PromptForTrackName(index);
                }
                break;
            case TrackListView.TrackAction.MoveUp:
                if (index > 0 && index < _song.Tracks.Count)
                {
                    var track = _song.Tracks[index];
                    _song.Tracks.RemoveAt(index);
                    _song.Tracks.Insert(index - 1, track);
                    _activeTrackIndex = index - 1; // Keep selection on the moved track
                }
                break;
            case TrackListView.TrackAction.MoveDown:
                if (index >= 0 && index < _song.Tracks.Count - 1)
                {
                    var track = _song.Tracks[index];
                    _song.Tracks.RemoveAt(index);
                    _song.Tracks.Insert(index + 1, track);
                    _activeTrackIndex = index + 1; // Keep selection on the moved track
                }
                break;
        }
    }

    private void PromptForTrackName(int trackIndex)
    {
        string newName = _song.Tracks[trackIndex].Name;
        
        Action<UIContext> drawCallback = (ctx) =>
        {
            UI.Text("rename_prompt", "Enter new track name:", new Vector2(10, 10));
            UI.InputText("rename_input", ref newName, new Vector2(280, 25), new Vector2(10, 40));
            
            UI.BeginHBoxContainer("rename_buttons", new Vector2(10, 80), 10);
            if (UI.Button("rename_ok", "OK", new Vector2(80, 25)))
            {
                _host.ModalWindowService.CloseModalWindow(0); // 0 = Success
            }
            if (UI.Button("rename_cancel", "Cancel", new Vector2(80, 25)))
            {
                _host.ModalWindowService.CloseModalWindow(1); // 1 = Cancel
            }
            UI.EndHBoxContainer();
        };

        _host.ModalWindowService.OpenModalWindow("Rename Track", 300, 120, drawCallback, (resultCode) =>
        {
            if (resultCode == 0 && !string.IsNullOrWhiteSpace(newName))
            {
                _song.Tracks[trackIndex].Name = newName;
            }
        });
    }

    private void DrawTopBar(Vector2 windowSize)
    {
        var topBarArea = new Rect(0, 0, windowSize.X, DawMetrics.TopBarHeight);
        var style = new BoxStyle { FillColor = DawTheme.PanelBackground, BorderColor = DawTheme.Border, BorderLengthBottom = 1, Roundness = 0 };
        UI.Context.Renderer.DrawBox(topBarArea, style);

        // Position menu bar and transport within the top bar
        _menuBarView.Draw(new Vector2(0, 0));
        _transportView.Draw(new Vector2(0, 30), _song);
    }
    
    private void DrawMainContent(Vector2 windowSize)
    {
        // Left Panel for Track List
        UI.BeginResizableVPanel("track_list_panel", ref _leftPanelWidth, HAlignment.Left, topOffset: DawMetrics.TopBarHeight, minWidth: 100, maxWidth: 300);
        _trackListView.Draw(_song, ref _activeTrackIndex);
        UI.EndResizableVPanel();

        // Right side for Timeline and Piano Roll
        float mainContentX = _leftPanelWidth;
        float mainContentWidth = windowSize.X - _leftPanelWidth;

        // Timeline
        var timelineArea = new Rect(mainContentX, DawMetrics.TopBarHeight, mainContentWidth, DawMetrics.TimelineHeight);
        _timelineView.Draw(timelineArea, _song, _pianoRollView.GetPanOffset(), _pianoRollView.GetZoom());

        // Toolbar for Piano Roll Tools
        float toolbarY = DawMetrics.TopBarHeight + DawMetrics.TimelineHeight;
        var toolbarArea = new Rect(mainContentX, toolbarY, mainContentWidth, DawMetrics.PianoRollToolbarHeight);
        _pianoRollToolbarView.Draw(toolbarArea, ref _currentTool);
        
        // Piano Roll
        float pianoRollY = toolbarY + DawMetrics.PianoRollToolbarHeight;
        var pianoRollArea = new Rect(mainContentX, pianoRollY, mainContentWidth, windowSize.Y - pianoRollY);
        
        var activeTrack = (_song.Tracks.Count > 0) ? _song.Tracks[_activeTrackIndex] : null;
        _pianoRollView.Draw(pianoRollArea, activeTrack, _song, _midiEngine.IsPlaying, _midiEngine.CurrentTimeMs, _currentTool);
    }
    
    public void SaveState()
    {
        SongSerializer.Save(_song, SongFilePath);
    }
}
