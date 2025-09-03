using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using DirectUI;
using DirectUI.Core;

namespace Sonorize;

public class AlbumsView
{
    private readonly MusicLibrary _musicLibrary;
    private readonly DataGridColumn[] _columns;
    private int _selectedIndex = -1;
    private List<AlbumInfo> _albums = new(); // Cache the processed list
    private int _lastLibraryFileCount = -1; // To check if we need to re-process
    private string? _currentArtistFilter;
    private readonly Action<AlbumInfo> _onAlbumSelected;

    public AlbumsView(MusicLibrary musicLibrary, Action<AlbumInfo> onAlbumSelected)
    {
        _musicLibrary = musicLibrary;
        _onAlbumSelected = onAlbumSelected;
        _columns =
        [
            new DataGridColumn("Album", 350, nameof(AlbumInfo.Name)),
            new DataGridColumn("Artist", 300, nameof(AlbumInfo.Artist)),
            new DataGridColumn("Year", 100, nameof(AlbumInfo.Year)),
            new DataGridColumn("Tracks", 100, nameof(AlbumInfo.TrackCount))
        ];
    }

    private void ProcessLibrary(string? artistFilter)
    {
        var currentFiles = _musicLibrary.Files;
        // Re-process if the file count or the filter has changed.
        if (currentFiles.Count == _lastLibraryFileCount && artistFilter == _currentArtistFilter) return;

        IEnumerable<MusicFile> filesToProcess = currentFiles;
        if (!string.IsNullOrEmpty(artistFilter))
        {
            filesToProcess = filesToProcess.Where(f => f.Artist == artistFilter);
        }

        _albums = filesToProcess
            .GroupBy(f => new { f.Album, f.Artist })
            .Select(g => new AlbumInfo
            {
                Name = g.Key.Album,
                Artist = g.Key.Artist,
                Year = g.FirstOrDefault()?.Year ?? 0,
                TrackCount = g.Count()
            })
            .OrderBy(a => a.Artist)
            .ThenBy(a => a.Name)
            .ToList();

        _lastLibraryFileCount = currentFiles.Count;
        _currentArtistFilter = artistFilter;
    }

    public void Draw(UIContext context, Vector2 position, Vector2 size, string? artistFilter)
    {
        ProcessLibrary(artistFilter);

        bool rowDoubleClicked;
        if (size.X > 0 && size.Y > 0)
        {
            UI.DataGrid<AlbumInfo>(
                "albumsGrid",
                _albums,
                _columns,
                ref _selectedIndex,
                size,
                out rowDoubleClicked,
                position,
                autoSizeColumns: true,
                trimCellText: true
            );

            if (rowDoubleClicked && _selectedIndex >= 0 && _selectedIndex < _albums.Count)
            {
                _onAlbumSelected?.Invoke(_albums[_selectedIndex]);
            }
        }
    }
}