using System.Numerics;
using DirectUI;
using DirectUI.Core;
using Sonorize.Audio;

namespace Sonorize;

public class LibraryView
{
    private readonly MusicLibrary _musicLibrary;
    private readonly PlaybackManager _playbackManager;
    private readonly Settings _settings;

    private int _selectedTrackIndex = -1;
    private readonly DataGridColumn[] _columns;

    public LibraryView(MusicLibrary musicLibrary, PlaybackManager playbackManager, Settings settings)
    {
        _musicLibrary = musicLibrary;
        _playbackManager = playbackManager;
        _settings = settings;

        _columns =
        [
            new DataGridColumn("Title", 350, nameof(MusicFile.Title)),
            new DataGridColumn("Artist", 250, nameof(MusicFile.Artist)),
            new DataGridColumn("Album", 250, nameof(MusicFile.Album)),
            new DataGridColumn("Duration", 100, nameof(MusicFile.Duration)),
            new DataGridColumn("Genre", 150, nameof(MusicFile.Genre)),
            new DataGridColumn("Year", 80, nameof(MusicFile.Year))
        ];
    }

    public void Draw(UIContext context, Vector2 position, Vector2 size)
    {
        // Keep the playback manager's tracklist in sync with the library.
        _playbackManager.SetTracklist(_musicLibrary.Files);

        int previousSelectedTrackIndex = _selectedTrackIndex;
        bool rowDoubleClicked = false;

        if (size.X > 0 && size.Y > 0)
        {
            UI.DataGrid<MusicFile>(
                "musicGrid",
                _musicLibrary.Files,
                _columns,
                ref _selectedTrackIndex,
                size,
                out rowDoubleClicked,
                position,
                autoSizeColumns: true,
                trimCellText: true
            );
        }

        bool selectionChanged = _selectedTrackIndex != previousSelectedTrackIndex;

        // Sync state between UI selection and playback manager
        if (_settings.PlayOnDoubleClick)
        {
            if (rowDoubleClicked)
            {
                _playbackManager.Play(_selectedTrackIndex);
            }
        }
        else // Play on single click
        {
            if (selectionChanged)
            {
                _playbackManager.Play(_selectedTrackIndex);
            }
        }

        if (_selectedTrackIndex != _playbackManager.CurrentTrackIndex)
        {
            // Playback manager changed the track (e.g., next song): update grid selection.
            _selectedTrackIndex = _playbackManager.CurrentTrackIndex;
        }
    }
}