namespace Sonorize.Services;

public class PlaylistService
{
    private readonly Settings _settings;
    private readonly Action _saveAction;

    public PlaylistService(Settings settings, Action saveAction)
    {
        _settings = settings;
        _saveAction = saveAction;
    }

    public void CreateNewPlaylist()
    {
        int num = _settings.Playlists.Count + 1;
        _settings.Playlists.Add(new Playlist { Name = $"New Playlist {num}" });
        _saveAction();
    }

    public void AddSongToPlaylist(MusicFile song, Playlist playlist)
    {
        if (!playlist.Tracks.Any(t => t.FilePath == song.FilePath))
        {
            playlist.Tracks.Add(song);
            _saveAction();
        }
    }
}