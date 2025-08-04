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

    // --- Child Views ---
    private readonly MenuBarView _menuBarView;
    private readonly TransportView _transportView;
    private readonly PianoRollView _pianoRollView;

    public DawAppLogic(IWindowHost host)
    {
        _midiEngine = new MidiEngine();
        _song = SongSerializer.Load(SongFilePath) ?? CreateDefaultSong();

        _menuBarView = new MenuBarView();
        _transportView = new TransportView(_midiEngine);
        _pianoRollView = new PianoRollView();
    }

    private static Song CreateDefaultSong()
    {
        var song = new Song { Tempo = 120 };
        // Basic C Major Scale
        song.Events.Add(new NoteEvent(0, 480, 60, 100));     // C
        song.Events.Add(new NoteEvent(500, 480, 62, 100));    // D
        song.Events.Add(new NoteEvent(1000, 480, 64, 100));   // E
        song.Events.Add(new NoteEvent(1500, 480, 65, 100));   // F
        song.Events.Add(new NoteEvent(2000, 480, 67, 100));   // G
        song.Events.Add(new NoteEvent(2500, 480, 69, 100));   // A
        song.Events.Add(new NoteEvent(3000, 480, 71, 100));   // B
        song.Events.Add(new NoteEvent(3500, 480, 72, 100));   // C
        return song;
    }

    public void DrawUI(UIContext context)
    {
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
                break;
            case MenuBarView.FileAction.Export:
                _midiEngine.ExportToMidiFile(_song, "export.mid");
                break;
        }

        // --- Draw UI Layout ---
        DrawTopBar(windowSize);
        DrawMainContent(windowSize);
    }

    private void DrawTopBar(Vector2 windowSize)
    {
        const float TopBarHeight = 70;
        var topBarArea = new Rect(0, 0, windowSize.X, TopBarHeight);
        var style = new BoxStyle { FillColor = DawTheme.PanelBackground, BorderColor = DawTheme.Border, BorderLengthBottom = 1, Roundness = 0 };
        UI.Context.Renderer.DrawBox(topBarArea, style);

        // Position menu bar and transport within the top bar
        _menuBarView.Draw(new Vector2(0, 0));
        _transportView.Draw(new Vector2(0, 30), _song);
    }

    private void DrawMainContent(Vector2 windowSize)
    {
        const float topOffset = 70; // Height of the top bar
        var mainContentArea = new Rect(0, topOffset, windowSize.X, windowSize.Y - topOffset);

        // The piano roll will take up the entire main content area for now
        _pianoRollView.Draw(mainContentArea, _song, _midiEngine.IsPlaying, _midiEngine.CurrentTimeMs);
    }
}