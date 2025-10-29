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
    private readonly Settings _settings;
    private readonly Action<Playlist> _onPlaylistSelected;
    private readonly Action _onCreatePlaylist;

    public PlaylistsView(MusicLibrary musicLibrary, Settings settings, Action<Playlist> onPlaylistSelected, Action onCreatePlaylist)
    {
        _musicLibrary = musicLibrary;
        _settings = settings;
        _onPlaylistSelected = onPlaylistSelected;
        _onCreatePlaylist = onCreatePlaylist;
    }

    public void Draw(UIContext context, Vector2 position, Vector2 size, string? searchText)
    {
        // This view is not part of the main navigation in the cloned design.
        // The playlists are now displayed in the sidebar.
        UI.Text("playlists_placeholder", "Playlists are available in the sidebar.", size,
            textAlignment: new Alignment(HAlignment.Center, VAlignment.Center));
    }
}