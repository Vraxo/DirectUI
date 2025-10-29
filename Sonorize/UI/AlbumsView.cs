using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using DirectUI;
using DirectUI.Core;

namespace Sonorize;

public class AlbumsView
{
    private readonly MusicLibrary _musicLibrary;
    private readonly Action<AlbumInfo> _onAlbumSelected;

    public AlbumsView(MusicLibrary musicLibrary, Action<AlbumInfo> onAlbumSelected)
    {
        _musicLibrary = musicLibrary;
        _onAlbumSelected = onAlbumSelected;
    }

    public void Draw(UIContext context, Vector2 position, Vector2 size, string? artistFilter, string? searchText)
    {
        // This view is not part of the main navigation in the cloned design.
        // Display a placeholder message.
        UI.Text("albums_placeholder", "Albums view not implemented in this version.", size,
            textAlignment: new Alignment(HAlignment.Center, VAlignment.Center));
    }
}