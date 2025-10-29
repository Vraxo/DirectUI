using System.Numerics;
using System.Text.Json;
using DirectUI;
using DirectUI.Backends.SkiaSharp;
using DirectUI.Core;
using Sonorize.Audio;

namespace Sonorize;

public class SonorizeLogic : IAppLogic
{
    private readonly IWindowHost _host;
    private Settings _settings;
    private readonly string _settingsFilePath = "settings.json";

    private readonly MenuBar _menuBar;
    private readonly SettingsWindow _settingsWindow;
    private readonly MusicLibrary _musicLibrary = new();
    private readonly PlaybackManager _playbackManager;
    private readonly PlayerBarView _playerBarView;
    private readonly LibraryView _libraryView;
    private readonly AlbumsView _albumsView;
    private readonly ArtistsView _artistsView;
    private readonly PlaylistsView _playlistsView;

    // --- Navigation State ---
    private int _activeSidebarIndex = 0; // 0 for Library, 1+ for playlists
    private string _searchText = "";
    private int _rightClickedSongIndex = -1;

    // --- Layout State ---
    private float _sidebarWidth = 230f;


    public SonorizeLogic(IWindowHost host)
    {
        _host = host;
        _settings = LoadState();

        ConfigureWindowStyles(host);

        _menuBar = new(OpenSettingsModal);
        _settingsWindow = new(_settings, _host, SaveState); // Pass Save action
        _playbackManager = new PlaybackManager(new AudioPlayer());
        _playerBarView = new PlayerBarView(_playbackManager, _settings);

        _libraryView = new LibraryView(_musicLibrary, _playbackManager, _settings, OnSongRightClicked);
        _albumsView = new AlbumsView(_musicLibrary, OnAlbumSelected);
        _artistsView = new ArtistsView(_musicLibrary, OnArtistSelected);
        _playlistsView = new PlaylistsView(_musicLibrary, _settings, OnPlaylistSelected, OnCreatePlaylist);

        // Start scanning for music files
        _musicLibrary.ScanDirectoriesAsync(_settings.Directories);
    }

    private Settings LoadState()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                string json = File.ReadAllText(_settingsFilePath);
                return JsonSerializer.Deserialize<Settings>(json) ?? new Settings();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading settings: {ex.Message}");
        }

        return new();
    }

    public void SaveState()
    {
        try
        {
            JsonSerializerOptions options = new()
            {
                WriteIndented = true
            };

            string json = JsonSerializer.Serialize(_settings, options);
            File.WriteAllText(_settingsFilePath, json);
            Console.WriteLine($"Settings saved to {_settingsFilePath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving settings: {ex.Message}");
        }
    }

    private void FinalSave()
    {
        SaveState();
        _playbackManager.Dispose();
    }

    void IAppLogic.SaveState()
    {
        FinalSave();
    }


    private static void ConfigureWindowStyles(IWindowHost host)
    {
        if (host is not SilkNetWindowHost silkHost)
        {
            return;
        }
        silkHost.BackdropType = WindowBackdropType.Default;
        silkHost.TitleBarTheme = WindowTitleBarTheme.Dark;
    }

    public void DrawUI(UIContext context)
    {
        _playbackManager.Update();

        float menuBarHeight = 0f; // No menubar in this design
        float playerBarHeight = 90f;
        float mainContentPadding = 0f;

        // Main layout using HBox
        var mainAreaPosition = new Vector2(0, menuBarHeight);
        var mainAreaSize = new Vector2(context.Renderer.RenderTargetSize.X, context.Renderer.RenderTargetSize.Y - menuBarHeight - playerBarHeight);
        UI.BeginHBoxContainer("mainLayout", mainAreaPosition, 0);
        {
            DrawSidebar(context, mainAreaSize.Y);
            DrawMainContent(context, mainAreaSize);
        }
        UI.EndHBoxContainer();

        _playerBarView.Draw(context);

        HandleContextMenu();
    }

    private void DrawSidebar(UIContext context, float availableHeight)
    {
        var sidebarBg = new BoxStyle
        {
            FillColor = new(0, 0, 0, 255),
            Roundness = 0,
            BorderLength = 0
        };

        var sidebarRect = new Vortice.Mathematics.Rect(0, 0, _sidebarWidth, availableHeight);
        context.Renderer.DrawBox(sidebarRect, sidebarBg);

        UI.BeginVBoxContainer("sidebarContent", new Vector2(10, 20), 5, new Vector2(_sidebarWidth - 20, 0));
        {
            // Logo
            UI.Text("logo", "Sonorize", style: new ButtonStyle { FontSize = 24 });
            UI.Separator(_sidebarWidth - 20, verticalPadding: 10);

            // Library Button
            var navButtonStyle = new ButtonStylePack { Roundness = 0.1f };
            navButtonStyle.Normal.FillColor = DirectUI.Drawing.Colors.Transparent;
            navButtonStyle.Active.FillColor = new(0.15f, 0.15f, 0.15f, 1f);
            navButtonStyle.Hover.FillColor = new(0.1f, 0.1f, 0.1f, 1f);
            navButtonStyle.ActiveHover.FillColor = new(0.15f, 0.15f, 0.15f, 1f);


            if (UI.Button("navLibrary", "Your Library", new Vector2(_sidebarWidth - 20, 30),
                theme: navButtonStyle, isActive: _activeSidebarIndex == 0,
                textAlignment: new Alignment(HAlignment.Left, VAlignment.Center), textMargin: new(10, 0)))
            {
                _activeSidebarIndex = 0;
            }

            // Playlists
            UI.Separator(_sidebarWidth - 20, verticalPadding: 10);
            UI.BeginHBoxContainer("playlistHeader", UI.Context.Layout.GetCurrentPosition(), 0, VAlignment.Center, 20);
            {
                UI.Text("playlistsLabel", "PLAYLISTS", style: new ButtonStyle { FontSize = 12, FontColor = new(0.7f, 0.7f, 0.7f, 1f) });

                // Spacer
                var currentPos = UI.Context.Layout.GetCurrentPosition();
                var textSize = context.TextService.MeasureText("PLAYLISTS", new ButtonStyle { FontSize = 12 });
                UI.Context.Layout.AdvanceLayout(new Vector2(_sidebarWidth - 20 - textSize.X - 30 - 10, 0)); // Hacky spacer

                if (UI.Button("addPlaylist", "+", new Vector2(20, 20), layer: 10))
                {
                    OnCreatePlaylist();
                }
            }
            UI.EndHBoxContainer();

            // Scrollable list of playlists
            UI.BeginScrollableRegion("playlistScroll", new Vector2(_sidebarWidth - 20, availableHeight - UI.Context.Layout.GetCurrentPosition().Y - 20), out float innerWidth);
            {
                for (int i = 0; i < _settings.Playlists.Count; i++)
                {
                    var playlist = _settings.Playlists[i];
                    if (UI.Button($"navPlaylist_{i}", playlist.Name, new Vector2(innerWidth, 30),
                        theme: navButtonStyle, isActive: _activeSidebarIndex == i + 1,
                        textAlignment: new Alignment(HAlignment.Left, VAlignment.Center), textMargin: new(10, 0)))
                    {
                        _activeSidebarIndex = i + 1;
                    }
                }
            }
            UI.EndScrollableRegion();
        }
        UI.EndVBoxContainer();
    }

    private void DrawMainContent(UIContext context, Vector2 mainAreaSize)
    {
        var contentWidth = mainAreaSize.X - _sidebarWidth;
        var contentHeight = mainAreaSize.Y;
        var contentPos = new Vector2(_sidebarWidth, 0);

        var contentBg = new BoxStyle { FillColor = new(18 / 255f, 18 / 255f, 18 / 255f, 1.0f), Roundness = 0, BorderLength = 0 };
        context.Renderer.DrawBox(new Vortice.Mathematics.Rect(contentPos.X, contentPos.Y, contentWidth, contentHeight), contentBg);

        var gridPos = new Vector2(contentPos.X + 10, contentPos.Y + 10);
        var gridSize = new Vector2(contentWidth - 20, contentHeight - 20);

        _libraryView.Draw(context, gridPos, gridSize, _activeSidebarIndex, _settings, _searchText);
    }

    private void OnSongRightClicked(int songIndexInView)
    {
        _rightClickedSongIndex = songIndexInView;
    }

    private void HandleContextMenu()
    {
        if (_rightClickedSongIndex == -1) return;

        var currentViewFiles = _libraryView.GetCurrentViewFiles();
        if (_rightClickedSongIndex >= currentViewFiles.Count)
        {
            _rightClickedSongIndex = -1;
            return;
        }

        var song = currentViewFiles[_rightClickedSongIndex];

        // This is a bit of a hack. Since context menus are popups, they are drawn at the end of the frame.
        // We can open another modal from within the context menu's drawing callback.
        if (UI.BeginContextMenu($"song_context_{song.FilePath}"))
        {
            int selectedItem = UI.ContextMenu("addToPlaylistMenu", ["Add to playlist..."]);
            if (selectedItem == 0)
            {
                OpenAddToPlaylistModal(song);
            }
        }
        _rightClickedSongIndex = -1;
    }

    private void OpenAddToPlaylistModal(MusicFile song)
    {
        _host.ModalWindowService.OpenModalWindow("Add to Playlist", 300, 400, (ctx) =>
        {
            UI.BeginVBoxContainer("addToPlaylistVBox", new(10, 10), 5);
            UI.Text("addToPlaylistHeader", $"Add '{song.Title}' to:", style: new ButtonStyle { FontSize = 16 });
            UI.Separator(280);

            UI.BeginScrollableRegion("playlistSelectScroll", new(280, 300), out float innerWidth);
            {
                foreach (var playlist in _settings.Playlists)
                {
                    if (UI.Button(playlist.Name, playlist.Name, new Vector2(innerWidth, 24), textAlignment: new Alignment(HAlignment.Left, VAlignment.Center)))
                    {
                        if (!playlist.Tracks.Any(t => t.FilePath == song.FilePath))
                        {
                            playlist.Tracks.Add(song);
                            SaveState();
                        }
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

    private void OnArtistSelected(string artistName) { }
    private void OnAlbumSelected(AlbumInfo album) { }
    private void OnPlaylistSelected(Playlist playlist) { }
    private void OnCreatePlaylist()
    {
        // For simplicity, create a new playlist with a default name directly.
        // A modal for naming it would be the next step.
        int num = _settings.Playlists.Count + 1;
        _settings.Playlists.Add(new Playlist { Name = $"New Playlist {num}" });
    }

    private void OpenSettingsModal()
    {
        _host.ModalWindowService.OpenModalWindow("Settings", 500, 400, _settingsWindow.Draw, resultCode =>
        {
            if (resultCode == 0) // OK was clicked
            {
                _musicLibrary.ScanDirectoriesAsync(_settings.Directories);
                SaveState();
            }
            else // Reload settings if canceled
            {
                _settings = LoadState();
            }
        });
    }
}