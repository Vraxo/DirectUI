using System.Collections.Generic;
using System.Linq;

namespace Sonorize;

public class Playlist
{
    public string Name { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public List<MusicFile> Tracks { get; } = new();
    public int TrackCount => Tracks.Count;
}