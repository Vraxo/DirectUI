using DirectUI;
using DirectUI.Core;
using Sonorize.Services;
using System.Linq;
using System.Numerics;

namespace Sonorize;

public class AppController
{
    private readonly IWindowHost _host;
    private readonly Settings _settings;
    private readonly UIStateService _uiState;
    private readonly LibraryView _libraryView;
    private readonly SettingsWindow _settingsWindow;
    private readonly MusicLibrary _musicLibrary;
    private readonly PlaylistService _playlistService;
    private readonly Action _saveStateCallback;

    public AppController(
        IWindowHost host,
        Settings settings,
        UIStateService uiState,
        LibraryView libraryView,
        SettingsWindow settingsWindow,
        MusicLibrary musicLibrary,
        PlaylistService playlistService,
        Action saveStateCallback)
    {
        _host = host;
        _settings = settings;
        _uiState = uiState;
        _libraryView = libraryView;
        _settingsWindow = settingsWindow;
        _musicLibrary = musicLibrary;
        _playlistService = playlistService;
        _saveStateCallback = saveStateCallback;
    }

    public void OnCreatePlaylist()
    {
        _playlistService.CreateNewPlaylist();
    }

    public void OnSongRightClicked(int songIndexInView)
    {
        _uiState.RightClickedSongIndex = songIndexInView;
    }

    public void HandleContextMenu()
    {
        if (_uiState.RightClickedSongIndex == -1) return;

        var currentViewFiles = _libraryView.GetCurrentViewFiles();
        if (_uiState.RightClickedSongIndex >= currentViewFiles.Count)
        {
            _uiState.RightClickedSongIndex = -1;
            return;
        }

        var song = currentViewFiles[_uiState.RightClickedSongIndex];

        if (UI.BeginContextMenu($"song_context_{song.FilePath}"))
        {
            int selectedItem = UI.ContextMenu("addToPlaylistMenu", ["Add to playlist..."]);
            if (selectedItem == 0)
            {
                OpenAddToPlaylistModal(song);
            }
        }
        _uiState.RightClickedSongIndex = -1;
    }

    private void OpenAddToPlaylistModal(MusicFile song)
    {
        _host.ModalWindowService.OpenModalWindow("Add to Playlist", 300, 400, (ctx) =>
        {
            UI.BeginVBoxContainer("addToPlaylistVBox", new(10, 10), 5);
            UI.Text("addToPlaylistHeader", $"Add '{song.Title}' to:", style: new ButtonStyle { FontSize = 16 });
            UI.Separator(280, 1f, 4f);

            UI.BeginScrollableRegion("playlistSelectScroll", new(280, 300), gap: 2);
            {
                foreach (var playlist in _settings.Playlists)
                {
                    if (UI.Button(playlist.Name, playlist.Name, new Vector2(280, 24), textAlignment: new Alignment(HAlignment.Left, VAlignment.Center)))
                    {
                        _playlistService.AddSongToPlaylist(song, playlist);
                        _host.ModalWindowService.CloseModalWindow(0);
                    }
                }
            }
            UI.EndScrollableRegion();

            if (UI.Button("cancelAddToPlaylist", "Cancel", new Vector2(80, 24)))
            {
                _host.ModalWindowService.CloseModalWindow(1);
            }
            UI.EndVBoxContainer();
        });
    }

    public void OpenSettingsModal()
    {
        _host.ModalWindowService.OpenModalWindow("Settings", 500, 400, _settingsWindow.Draw, resultCode =>
        {
            if (resultCode == 0) // OK was clicked
            {
                _musicLibrary.ScanDirectoriesAsync(_settings.Directories);
                _saveStateCallback();
            }
            // If canceled, the settings window handles reverting its own state.
        });
    }
}