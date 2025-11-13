using System.Collections.Generic;

namespace Sonorize;

public enum TextOverflowMode { Wrap, Truncate }

public class Settings
{
    public List<string> Directories { get; set; } = new();
    public bool PlayOnDoubleClick { get; set; } = true;
    public bool UseCompactPlaybackControls { get; set; } = false;
    public bool UseLargeAlbumArt { get; set; } = false;
    public List<Playlist> Playlists { get; set; } = new();

    // Appearance settings from WebSharp version
    public TextOverflowMode TrackListOverflow { get; set; } = TextOverflowMode.Truncate;
    public int CompactnessLevel { get; set; } = 2; // 1=Compact, 2=Default, 3=Comfortable
    public bool EnableZebraStriping { get; set; } = true;
    public float Volume { get; set; } = 0.75f;
}