using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using DirectUI;
using DirectUI.Core;

namespace Sonorize;

public class ArtistsView
{
    private readonly MusicLibrary _musicLibrary;
    private readonly Action<string> _onArtistSelected;

    public ArtistsView(MusicLibrary musicLibrary, Action<string> onArtistSelected)
    {
        _musicLibrary = musicLibrary;
        _onArtistSelected = onArtistSelected;
    }

    public void Draw(UIContext context, Vector2 position, Vector2 size, string? searchText)
    {
        // This view is not part of the main navigation in the cloned design.
        // Display a placeholder message.
        UI.Text("artists_placeholder", "Artists view not implemented in this version.", size,
            textAlignment: new Alignment(HAlignment.Center, VAlignment.Center));
    }
}