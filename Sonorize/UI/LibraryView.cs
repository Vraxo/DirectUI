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
    private readonly Action<int> _onSongRightClicked;

    private int _selectedTrackIndex = -1;
    private readonly DataGridColumn[] _columns;
    private IReadOnlyList<MusicFile> _currentViewFiles = new List<MusicFile>();

    public IReadOnlyList<MusicFile> GetCurrentViewFiles() => _currentViewFiles;

    public LibraryView(MusicLibrary musicLibrary, PlaybackManager playbackManager, Settings settings, Action<int> onSongRightClicked)
    {
        _musicLibrary = musicLibrary;
        _playbackManager = playbackManager;
        _settings = settings;
        _onSongRightClicked = onSongRightClicked;

        _columns =
        [
            new DataGridColumn("Title", 350, nameof(MusicFile.Title)),
            new DataGridColumn("Artist", 250, nameof(MusicFile.Artist)),
            new DataGridColumn("Album", 250, nameof(MusicFile.Album)),
            new DataGridColumn("Duration", 100, nameof(MusicFile.Duration)),
        ];
    }

    public void Draw(UIContext context, Vector2 position, Vector2 size, int activeSidebarIndex, Settings settings, string? searchText)
    {
        // Determine which list of files to show
        if (activeSidebarIndex == 0) // "Your Library"
        {
            _currentViewFiles = _musicLibrary.Files;
        }
        else // A playlist is selected
        {
            int playlistIndex = activeSidebarIndex - 1;
            if (playlistIndex >= 0 && playlistIndex < settings.Playlists.Count)
            {
                _currentViewFiles = settings.Playlists[playlistIndex].Tracks;
            }
            else
            {
                _currentViewFiles = new List<MusicFile>(); // Should not happen
            }
        }

        // Apply search text filter on top
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            _currentViewFiles = _currentViewFiles.Where(f =>
                f.Title.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                f.Artist.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                f.Album.Contains(searchText, StringComparison.OrdinalIgnoreCase)).ToList();
        }

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
                trimCellText: settings.TrackListOverflow == TextOverflowMode.Truncate,
                rowVerticalPadding: settings.CompactnessLevel,
                enableZebraStriping: settings.EnableZebraStriping,
                onRowRightClick: _onSongRightClicked
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