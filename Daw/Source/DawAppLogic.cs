using System.Drawing;
using System.Linq;
using System.Numerics;
using Daw.Audio;
using Daw.Core;
using DirectUI;
using DirectUI.Core;
using Vortice.Mathematics;

namespace Daw;

public class DawAppLogic : IAppLogic
{
    private readonly MidiEngine _midiEngine;
    private Song _song;
    private const string SongFilePath = "mysong.dawjson";

    public DawAppLogic(IWindowHost host)
    {
        _midiEngine = new MidiEngine();
        _song = SongSerializer.Load(SongFilePath) ?? CreateDefaultSong();
    }

    private static Song CreateDefaultSong()
    {
        return new Song
        {
            Tempo = 120,
            Events = new()
            {
                new NoteEvent(0, 500, 60, 100),
                new NoteEvent(500, 500, 62, 100),
                new NoteEvent(1000, 500, 64, 100),
                new NoteEvent(1500, 500, 65, 100),
            }
        };
    }

    public void DrawUI(UIContext context)
    {
        var windowSize = context.Renderer.RenderTargetSize;
        DrawMenuBar(windowSize);
        DrawTimeline(windowSize);
    }

    private void DrawMenuBar(Vector2 windowSize)
    {
        UI.BeginHBoxContainer("menubar", new Vector2(5, 5), 5);

        if (UI.Button("play_button", "Play"))
        {
            _midiEngine.Play(_song);
        }

        if (UI.Button("stop_button", "Stop"))
        {
            _midiEngine.Stop();
        }

        UI.Separator(1, 20, 0);

        if (UI.Button("add_note_button", "Add Note"))
        {
            int lastNoteTime = _song.Events.Any() ? _song.Events.Max(e => e.StartTimeMs + e.DurationMs) : 0;
            _song.Events.Add(new NoteEvent(lastNoteTime, 500, 60, 100));
        }

        if (UI.Button("save_button", "Save"))
        {
            SongSerializer.Save(_song, SongFilePath);
        }

        if (UI.Button("load_button", "Load"))
        {
            _midiEngine.Stop();
            _song = SongSerializer.Load(SongFilePath) ?? CreateDefaultSong();
        }

        if (UI.Button("export_button", "Export MIDI"))
        {
            _midiEngine.ExportToMidiFile(_song, "export.mid");
        }

        UI.EndHBoxContainer();
    }

    private void DrawTimeline(Vector2 windowSize)
    {
        var timelineArea = new Rect(10, 40, windowSize.X - 20, windowSize.Y - 50);
        UI.Context.Renderer.DrawBox(timelineArea, new BoxStyle { FillColor = new Color4(0.1f, 0.1f, 0.1f, 1f), BorderColor = Colors.Gray, BorderLength = 1 });

        float pixelsPerMs = 0.2f;
        float noteHeight = 20f;
        int maxPitch = 84;
        int minPitch = 48;
        int pitchRange = maxPitch - minPitch;
        if (pitchRange <= 0) return;

        foreach (var note in _song.Events)
        {
            float x = timelineArea.X + (note.StartTimeMs * pixelsPerMs);
            float width = note.DurationMs * pixelsPerMs;

            float yRatio = (float)(maxPitch - note.Pitch) / pitchRange;
            float y = timelineArea.Y + (yRatio * timelineArea.Height);

            y = Math.Clamp(y, timelineArea.Top, timelineArea.Bottom - noteHeight);

            var noteRect = new Rect(x, y, width, noteHeight);

            if (noteRect.Right < timelineArea.X || noteRect.X > timelineArea.Right)
            {
                continue;
            }

            var noteStyle = new BoxStyle { FillColor = Colors.CornflowerBlue, BorderColor = Colors.LightBlue, BorderLength = 1, Roundness = 0.1f };
            UI.Context.Renderer.DrawBox(noteRect, noteStyle);
        }
    }
}