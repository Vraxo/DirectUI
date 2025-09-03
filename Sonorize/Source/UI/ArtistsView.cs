using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using DirectUI;
using DirectUI.Core;

namespace Sonorize;

public class ArtistsView
{
    private readonly MusicLibrary _musicLibrary;
    private readonly DataGridColumn[] _columns;
    private int _selectedIndex = -1;
    private List<ArtistInfo> _artists = new(); // Cache
    private int _lastLibraryFileCount = -1;
    private readonly Action<string> _onArtistSelected;

    public ArtistsView(MusicLibrary musicLibrary, Action<string> onArtistSelected)
    {
        _musicLibrary = musicLibrary;
        _onArtistSelected = onArtistSelected;
        _columns =
        [
            new DataGridColumn("Artist", 400, nameof(ArtistInfo.Name)),
            new DataGridColumn("Albums", 150, nameof(ArtistInfo.AlbumCount)),
            new DataGridColumn("Tracks", 150, nameof(ArtistInfo.TrackCount))
        ];
    }

    private void ProcessLibrary()
    {
        var currentFiles = _musicLibrary.Files;
        if (currentFiles.Count == _lastLibraryFileCount) return;

        _artists = currentFiles
            .GroupBy(f => f.Artist)
            .Select(g => new ArtistInfo
            {
                Name = g.Key,
                AlbumCount = g.Select(f => f.Album).Distinct().Count(),
                TrackCount = g.Count()
            })
            .OrderBy(a => a.Name)
            .ToList();

        _lastLibraryFileCount = currentFiles.Count;
    }

    public void Draw(UIContext context, Vector2 position, Vector2 size)
    {
        ProcessLibrary();

        bool rowDoubleClicked;
        if (size.X > 0 && size.Y > 0)
        {
            UI.DataGrid<ArtistInfo>(
                "artistsGrid",
                _artists,
                _columns,
                ref _selectedIndex,
                size,
                out rowDoubleClicked,
                position,
                autoSizeColumns: true,
                trimCellText: true
            );

            if (rowDoubleClicked && _selectedIndex >= 0 && _selectedIndex < _artists.Count)
            {
                _onArtistSelected?.Invoke(_artists[_selectedIndex].Name);
            }
        }
    }
}