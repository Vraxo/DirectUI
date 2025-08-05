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
    private readonly AudioEngine _audioEngine;
    private readonly MidiEngine _midiEngine;
    private Song _song;
    private const string SongFilePath = "mysong.dawjson";

    // --- UI State ---
    private readonly SynthParameters _synthParameters = new();
    private int _activeTrackIndex = 0;
    private float _leftPanelWidth = 200; // Widen for synth controls
    private float _bottomPanelHeight = 100;
    private PianoRollTool _currentTool = PianoRollTool.Select;

    // --- Child Views ---
    private readonly MenuBarView _menuBarView;
    private readonly TransportView _transportView;
    private readonly TimelineView _timelineView; // FIX: Restored this line
    private readonly TrackListView _trackListView;
    private readonly PianoRollToolbarView _pianoRollToolbarView;
    private readonly PianoRollView _pianoRollView;
    private readonly SynthControlView _synthControlView; // New view for synth controls
    private readonly IWindowHost _host;

    private class RenameModalState
    {
        public string Name;
        public RenameModalState(string initialName) { Name = initialName; }
    }

    public DawAppLogic(IWindowHost host)
    {
        _host = host;
        _audioEngine = new AudioEngine(_synthParameters); // Pass parameters to the engine
        _midiEngine = new MidiEngine(_audioEngine);
        _song = SongSerializer.Load(SongFilePath) ?? CreateDefaultSong();

        _menuBarView = new MenuBarView();
        _transportView = new TransportView(_midiEngine);
        _timelineView = new TimelineView(); // FIX: Restored this line
        _trackListView = new TrackListView();
        _pianoRollToolbarView = new PianoRollToolbarView();
        _pianoRollView = new PianoRollView();
        _synthControlView = new SynthControlView(); // Instantiate the new view
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
        _midiEngine.Update();
        var windowSize = context.Renderer.RenderTargetSize;

        if (_activeTrackIndex >= _song.Tracks.Count)
        {
            _activeTrackIndex = Math.Max(0, _song.Tracks.Count - 1);
        }

        DrawTopBar(windowSize);
        DrawMainContent(windowSize);

        HandleGlobalActions();
        HandleTrackActions();
    }

    private void HandleGlobalActions()
    {
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
    }

    private void HandleTrackActions()
    {
        var (action, index) = _trackListView.GetAction();
        if (action == TrackListView.TrackAction.None) return;

        switch (action)
        {
            case TrackListView.TrackAction.Delete:
                if (index >= 0 && index < _song.Tracks.Count) _song.Tracks.RemoveAt(index);
                break;
            case TrackListView.TrackAction.Rename:
                if (index >= 0 && index < _song.Tracks.Count) PromptForTrackName(index);
                break;
            case TrackListView.TrackAction.MoveUp:
                if (index > 0 && index < _song.Tracks.Count)
                {
                    var track = _song.Tracks[index];
                    _song.Tracks.RemoveAt(index);
                    _song.Tracks.Insert(index - 1, track);
                    _activeTrackIndex = index - 1;
                }
                break;
            case TrackListView.TrackAction.MoveDown:
                if (index >= 0 && index < _song.Tracks.Count - 1)
                {
                    var track = _song.Tracks[index];
                    _song.Tracks.RemoveAt(index);
                    _song.Tracks.Insert(index + 1, track);
                    _activeTrackIndex = index + 1;
                }
                break;
        }
    }

    private void PromptForTrackName(int trackIndex)
    {
        var modalState = new RenameModalState(_song.Tracks[trackIndex].Name);
        Action<UIContext> drawCallback = (ctx) =>
        {
            UI.BeginVBoxContainer("rename_vbox", new Vector2(10, 10), 15);
            UI.Text("rename_prompt", "Enter new track name:");
            UI.InputText("rename_input", ref modalState.Name, new Vector2(280, 25));
            UI.BeginHBoxContainer("rename_buttons", UI.Context.Layout.GetCurrentPosition(), 10);
            if (UI.Button("rename_ok", "OK", new Vector2(80, 25))) _host.ModalWindowService.CloseModalWindow(0);
            if (UI.Button("rename_cancel", "Cancel", new Vector2(80, 25))) _host.ModalWindowService.CloseModalWindow(1);
            UI.EndHBoxContainer();
            UI.EndVBoxContainer();
        };
        Action<int> onClosedCallback = (resultCode) =>
        {
            if (resultCode == 0 && !string.IsNullOrWhiteSpace(modalState.Name))
            {
                _song.Tracks[trackIndex].Name = modalState.Name;
            }
        };
        _host.ModalWindowService.OpenModalWindow("Rename Track", 300, 160, drawCallback, onClosedCallback);
    }

    private void DrawTopBar(Vector2 windowSize)
    {
        var topBarArea = new Rect(0, 0, windowSize.X, DawMetrics.TopBarHeight);
        var style = new BoxStyle { FillColor = DawTheme.PanelBackground, BorderColor = DawTheme.Border, BorderLengthBottom = 1, Roundness = 0 };
        UI.Context.Renderer.DrawBox(topBarArea, style);
        _menuBarView.Draw(new Vector2(0, 0));
        _transportView.Draw(new Vector2(150, 30), _song); // Offset to not overlap menu
    }

    private void DrawMainContent(Vector2 windowSize)
    {
        // Left Panel for Track List and Synth Controls
        UI.BeginResizableVPanel("left_panel", ref _leftPanelWidth, HAlignment.Left, topOffset: DawMetrics.TopBarHeight, minWidth: 150, maxWidth: 400);
        UI.BeginVBoxContainer("left_panel_vbox", new Vector2(5, 5), 10);
        _trackListView.Draw(_song, ref _activeTrackIndex);
        UI.Separator(100, 1, 10);
        _synthControlView.Draw(new Rect(0, UI.Context.Layout.GetCurrentPosition().Y, _leftPanelWidth - 10, 350), _synthParameters);
        UI.EndVBoxContainer();
        UI.EndResizableVPanel();

        // Right side for Timeline and Piano Roll
        float mainContentX = _leftPanelWidth;
        float mainContentWidth = windowSize.X - _leftPanelWidth;

        UI.BeginResizableHPanel("editor_panel", ref _bottomPanelHeight, reservedLeftSpace: mainContentX, reservedRightSpace: 0, topOffset: DawMetrics.TopBarHeight, minHeight: 50, maxHeight: 300);
        var velocityPaneArea = new Rect(mainContentX, windowSize.Y - _bottomPanelHeight, mainContentWidth, _bottomPanelHeight);
        UI.Context.Renderer.DrawBox(velocityPaneArea, new BoxStyle { FillColor = DawTheme.Background, Roundness = 0 });
        _pianoRollView.DrawVelocityPane(velocityPaneArea, _song);
        UI.EndResizableHPanel();

        float upperAreaHeight = windowSize.Y - DawMetrics.TopBarHeight - _bottomPanelHeight;
        if (upperAreaHeight < 0) upperAreaHeight = 0;

        var timelineArea = new Rect(mainContentX, DawMetrics.TopBarHeight, mainContentWidth, DawMetrics.TimelineHeight);
        if (timelineArea.Bottom <= upperAreaHeight + DawMetrics.TopBarHeight)
        {
            _timelineView.Draw(timelineArea, _song, _pianoRollView.GetPanOffset(), _pianoRollView.GetZoom());
        }

        float toolbarY = DawMetrics.TopBarHeight + DawMetrics.TimelineHeight;
        var toolbarArea = new Rect(mainContentX, toolbarY, mainContentWidth, DawMetrics.PianoRollToolbarHeight);
        if (toolbarArea.Bottom <= upperAreaHeight + DawMetrics.TopBarHeight)
        {
            _pianoRollToolbarView.Draw(toolbarArea, ref _currentTool);
        }

        float pianoRollY = toolbarY + DawMetrics.PianoRollToolbarHeight;
        float availablePianoRollHeight = windowSize.Y - pianoRollY - _bottomPanelHeight;
        if (availablePianoRollHeight > 0)
        {
            var pianoRollArea = new Rect(mainContentX, pianoRollY, mainContentWidth, availablePianoRollHeight);
            var activeTrack = (_song.Tracks.Count > 0) ? _song.Tracks[_activeTrackIndex] : null;
            _pianoRollView.Draw(pianoRollArea, activeTrack, _song, _midiEngine.IsPlaying, _midiEngine.CurrentTimeMs, _currentTool);
        }
    }

    public void SaveState()
    {
        SongSerializer.Save(_song, SongFilePath);
        _midiEngine.Dispose();
        _audioEngine.Dispose();
    }
}