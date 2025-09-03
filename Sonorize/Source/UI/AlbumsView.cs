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

    public AlbumsView(MusicLibrary musicLibrary)
    {
        _musicLibrary = musicLibrary;
        _columns =
        [
            new DataGridColumn("Album", 350, nameof(AlbumInfo.Name)),
            new DataGridColumn("Artist", 300, nameof(AlbumInfo.Artist)),
            new DataGridColumn("Year", 100, nameof(AlbumInfo.Year)),
            new DataGridColumn("Tracks", 100, nameof(AlbumInfo.TrackCount))
        ];
    }

    private void ProcessLibrary()
    {
        var currentFiles = _musicLibrary.Files;
        // Simple check to see if files have changed. Using count as a proxy.
        if (currentFiles.Count == _lastLibraryFileCount) return;

        _albums = currentFiles
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
    }

    public void Draw(UIContext context, Vector2 position, Vector2 size)
    {
        ProcessLibrary();

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
        }
    }
}