using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using DirectUI;
using DirectUI.Core;

namespace Sonorize;

public class PlaylistsView
{
    private readonly MusicLibrary _musicLibrary;
    private readonly DataGridColumn[] _columns;
    private int _selectedIndex = -1;
    private List<Playlist> _playlists = new();
    private int _lastPlaylistCount = -1;
    private string? _currentSearchText;
    private readonly Action<Playlist> _onPlaylistSelected;

    public PlaylistsView(MusicLibrary musicLibrary, Action<Playlist> onPlaylistSelected)
    {
        _musicLibrary = musicLibrary;
        _onPlaylistSelected = onPlaylistSelected;
        _columns =
        [
            new DataGridColumn("Playlist", 600, nameof(Playlist.Name)),
            new DataGridColumn("Tracks", 150, nameof(Playlist.TrackCount))
        ];
    }

    private void ProcessLibrary(string? searchText)
    {
        IReadOnlyList<Playlist> currentPlaylists = _musicLibrary.Playlists;
        if (currentPlaylists.Count == _lastPlaylistCount && searchText == _currentSearchText) return;

        IEnumerable<Playlist> processedPlaylists = currentPlaylists;

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            processedPlaylists = processedPlaylists.Where(p =>
                p.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase));
        }

        _playlists = processedPlaylists.OrderBy(p => p.Name).ToList();

        _lastPlaylistCount = currentPlaylists.Count;
        _currentSearchText = searchText;
    }

    public void Draw(UIContext context, Vector2 position, Vector2 size, string? searchText)
    {
        ProcessLibrary(searchText);

        bool rowDoubleClicked;
        if (size.X > 0 && size.Y > 0)
        {
            UI.DataGrid<Playlist>(
                "playlistsGrid",
                _playlists,
                _columns,
                ref _selectedIndex,
                size,
                out rowDoubleClicked,
                position,
                autoSizeColumns: true,
                trimCellText: true
            );

            if (rowDoubleClicked && _selectedIndex >= 0 && _selectedIndex < _playlists.Count)
            {
                _onPlaylistSelected?.Invoke(_playlists[_selectedIndex]);
            }
        }
    }
}