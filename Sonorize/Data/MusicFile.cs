// Sonorize/Source/Data/MusicFile.cs
namespace Sonorize;

public class MusicFile
{
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string Album { get; set; } = string.Empty;
    public string Genre { get; set; } = string.Empty;
    public uint Year { get; set; }
    public TimeSpan Duration { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public byte[]? AlbumArt { get; set; }
    public byte[]? AbstractAlbumArt { get; set; }
}