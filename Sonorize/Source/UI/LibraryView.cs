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
    private IReadOnlyList<MusicFile> _currentViewFiles = new List<MusicFile>();

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

    public void Draw(UIContext context, Vector2 position, Vector2 size, AlbumInfo? albumFilter, Playlist? playlistFilter, string? searchText)
    {
        IEnumerable<MusicFile> filesToDisplay;

        // Highest priority filter is the playlist
        if (playlistFilter != null)
        {
            filesToDisplay = playlistFilter.Tracks;
        }
        else if (albumFilter != null)
        {
            filesToDisplay = _musicLibrary.Files
                .Where(f => f.Artist == albumFilter.Artist && f.Album == albumFilter.Name);
        }
        else
        {
            filesToDisplay = _musicLibrary.Files;
        }

        // Apply search text filter on top
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            filesToDisplay = filesToDisplay.Where(f =>
                f.Title.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                f.Artist.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                f.Album.Contains(searchText, StringComparison.OrdinalIgnoreCase));
        }

        _currentViewFiles = filesToDisplay.ToList();

        // Keep the playback manager's tracklist in sync with the *currently viewed* list.
        _playbackManager.SetTracklist(_currentViewFiles);

        int previousSelectedTrackIndex = _selectedTrackIndex;
        bool rowDoubleClicked = false;

        if (size.X > 0 && size.Y > 0)
        {
            UI.DataGrid<MusicFile>(
                "musicGrid",
                _currentViewFiles,
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

        // This ensures the grid selection updates if the song changes automatically (e.g., `Next()`).
        // The index here is relative to the `_currentViewFiles` list.
        if (_selectedTrackIndex != _playbackManager.CurrentTrackIndex)
        {
            _selectedTrackIndex = _playbackManager.CurrentTrackIndex;
        }
    }
}