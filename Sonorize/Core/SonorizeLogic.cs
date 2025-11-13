using System.Numerics;
using System.Text.Json;
using DirectUI;
using DirectUI.Backends.SkiaSharp;
using DirectUI.Core;
using DirectUI.Drawing;
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
    private bool _isSidebarCollapsed = false;
    private const float SidebarWidthExpanded = 230f;
    private const float SidebarWidthCollapsed = 68f;
    private float SidebarWidth => _isSidebarCollapsed ? SidebarWidthCollapsed : SidebarWidthExpanded;


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

        // Main layout using manual positioning for the two main panels
        var mainAreaPosition = new Vector2(0, menuBarHeight);
        var mainAreaSize = new Vector2(context.Renderer.RenderTargetSize.X, context.Renderer.RenderTargetSize.Y - menuBarHeight - playerBarHeight);

        DrawSidebar(context, mainAreaSize.Y);
        DrawMainContent(context, mainAreaSize);

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

        var sidebarRect = new Vortice.Mathematics.Rect(0, 0, SidebarWidth, availableHeight);
        context.Renderer.DrawBox(sidebarRect, sidebarBg);

        float innerWidth = SidebarWidth - 20;
        if (innerWidth < 0) innerWidth = 0;

        UI.BeginVBoxContainer("sidebarContent", new Vector2(10, 20), 5, new Vector2(innerWidth, 0));
        {
            // Logo
            if (_isSidebarCollapsed)
            {
                var logoIconStyle = new ButtonStyle { FontSize = 24, FontWeight = Vortice.DirectWrite.FontWeight.Bold };
                UI.Text("logo", "S", new Vector2(innerWidth, 24), style: logoIconStyle, textAlignment: new Alignment(HAlignment.Center, VAlignment.Center));
            }
            else
            {
                UI.Text("logo", "Sonorize", style: new ButtonStyle { FontSize = 24 });
            }
            UI.Separator(innerWidth, 1f, 10f);

            // --- Navigation Buttons Theme ---
            var navButtonStyle = new ButtonStylePack { Roundness = 0.1f };
            navButtonStyle.Normal.FillColor = Colors.Transparent;
            navButtonStyle.Active.FillColor = new(0.15f, 0.15f, 0.15f, 1f);
            navButtonStyle.Hover.FillColor = new(0.1f, 0.1f, 0.1f, 1f);
            navButtonStyle.ActiveHover.FillColor = new(0.15f, 0.15f, 0.15f, 1f);
            navButtonStyle.FontName = _isSidebarCollapsed ? "FontAwesomeSolid" : "Segoe UI";

            // --- Library Button ---
            var libraryButtonText = _isSidebarCollapsed ? "\uF02D" : "Your Library"; // fa-book
            var libraryTextAlignment = _isSidebarCollapsed ? new Alignment(HAlignment.Center, VAlignment.Center) : new Alignment(HAlignment.Left, VAlignment.Center);

            if (UI.Button("navLibrary", libraryButtonText, new Vector2(innerWidth, 30),
                theme: navButtonStyle, isActive: _activeSidebarIndex == 0,
                textAlignment: libraryTextAlignment, textMargin: _isSidebarCollapsed ? Vector2.Zero : new(10, 0)))
            {
                _activeSidebarIndex = 0;
            }

            // Playlists
            UI.Separator(innerWidth, 1f, 10f);
            UI.BeginHBoxContainer("playlistHeader", UI.Context.Layout.GetCurrentPosition(), 5, VAlignment.Center, 20);
            {
                if (!_isSidebarCollapsed)
                {
                    var textStyle = new ButtonStyle { FontSize = 12, FontColor = new(0.7f, 0.7f, 0.7f, 1f) };
                    var textSize = context.TextService.MeasureText("PLAYLISTS", textStyle);
                    UI.Text("playlistsLabel", "PLAYLISTS", style: textStyle);

                    var addButtonSize = new Vector2(20, 20);
                    float spacerWidth = innerWidth - textSize.X - 5 - addButtonSize.X;
                    if (spacerWidth > 0)
                    {
                        UI.Context.Layout.AdvanceLayout(new Vector2(spacerWidth, 0));
                    }
                }

                var addPlaylistSize = _isSidebarCollapsed ? new Vector2(innerWidth, 20) : new Vector2(20, 20);
                if (UI.Button("addPlaylist", "+", addPlaylistSize, layer: 10))
                {
                    OnCreatePlaylist();
                }
            }
            UI.EndHBoxContainer();

            // Scrollable list of playlists
            float collapseButtonHeight = 50; // Reserve space for collapse button
            float scrollHeight = availableHeight - UI.Context.Layout.GetCurrentPosition().Y - collapseButtonHeight;
            if (scrollHeight < 0) scrollHeight = 0;

            UI.BeginScrollableRegion("playlistScroll", new Vector2(innerWidth, scrollHeight), gap: 2);
            {
                float scrollInnerWidth = UI.GetAvailableContentWidth();

                for (int i = 0; i < _settings.Playlists.Count; i++)
                {
                    var playlist = _settings.Playlists[i];
                    var playlistIcon = "\uF001"; // fa-music
                    var playlistText = _isSidebarCollapsed ? playlistIcon : playlist.Name;
                    var playlistAlignment = _isSidebarCollapsed ? new Alignment(HAlignment.Center, VAlignment.Center) : new Alignment(HAlignment.Left, VAlignment.Center);

                    if (UI.Button($"navPlaylist_{i}", playlistText, new Vector2(scrollInnerWidth, 30),
                        theme: navButtonStyle, isActive: _activeSidebarIndex == i + 1,
                        textAlignment: playlistAlignment, textMargin: _isSidebarCollapsed ? Vector2.Zero : new(10, 0)))
                    {
                        _activeSidebarIndex = i + 1;
                    }
                }
            }
            UI.EndScrollableRegion();
        }
        UI.EndVBoxContainer();

        // --- Sidebar Footer ---
        var collapseButtonSize = new Vector2(32, 32);
        var collapseButtonPos = new Vector2((SidebarWidth - collapseButtonSize.X) / 2, availableHeight - collapseButtonSize.Y - 10);
        var collapseIcon = _isSidebarCollapsed ? "\uF054" : "\uF053"; // right chevron : left chevron

        var collapseTheme = new ButtonStylePack { FontName = "FontAwesomeSolid", FontSize = 14, Roundness = 1f };
        collapseTheme.Normal.FillColor = Colors.Transparent;
        collapseTheme.Hover.FillColor = new Color(0.2f, 0.2f, 0.2f, 1f);

        if (UI.Button("collapseSidebar", collapseIcon, collapseButtonSize, theme: collapseTheme, origin: collapseButtonPos))
        {
            _isSidebarCollapsed = !_isSidebarCollapsed;
        }
    }

    private void DrawMainContent(UIContext context, Vector2 mainAreaSize)
    {
        var contentWidth = mainAreaSize.X - SidebarWidth;
        var contentHeight = mainAreaSize.Y;
        var contentPos = new Vector2(SidebarWidth, 0);

        // Draw background
        var contentBg = new BoxStyle { FillColor = new(18 / 255f, 18 / 255f, 18 / 255f, 1.0f), Roundness = 0, BorderLength = 0 };
        context.Renderer.DrawBox(new Vortice.Mathematics.Rect(contentPos.X, contentPos.Y, contentWidth, contentHeight), contentBg);

        // --- Header Section ---
        float headerHeight = 40f;
        var headerPos = contentPos + new Vector2(20, 10);
        var headerWidth = contentWidth - 40;

        UI.BeginHBoxContainer("mainHeader", headerPos, 5, VAlignment.Center, headerHeight);
        {
            // Title
            string title = "Your Library";
            if (_activeSidebarIndex > 0 && _activeSidebarIndex - 1 < _settings.Playlists.Count)
            {
                title = _settings.Playlists[_activeSidebarIndex - 1].Name;
            }
            UI.Text("mainContentTitle", title, style: new ButtonStyle { FontSize = 24 });

            // Spacer to push button to the right
            var titleStyle = new ButtonStyle { FontSize = 24 };
            var titleSize = context.TextService.MeasureText(title, titleStyle);
            var settingsButtonSize = new Vector2(30, 30);
            float spacerWidth = headerWidth - titleSize.X - settingsButtonSize.X;
            if (spacerWidth > 0)
            {
                UI.Context.Layout.AdvanceLayout(new Vector2(spacerWidth, 0));
            }

            // Settings Button
            var theme = new ButtonStylePack { FontSize = 16, FontName = "Segoe UI Emoji", Roundness = 1f };
            theme.Normal.FillColor = Colors.Transparent;
            theme.Hover.FillColor = new Color(0.2f, 0.2f, 0.2f, 1f);
            if (UI.Button("settingsButton", "⚙️", settingsButtonSize, theme: theme, layer: 10))
            {
                OpenSettingsModal();
            }
        }
        UI.EndHBoxContainer();
        // --- End Header Section ---

        // Adjust grid position to be below the header
        var gridPos = new Vector2(contentPos.X + 10, headerPos.Y + headerHeight);
        var gridSize = new Vector2(
            contentWidth - 20,
            contentHeight - headerHeight - 20 // 10px top padding (from header) + 10px bottom
        );

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
            UI.Separator(280, 1f, 4f);

            UI.BeginScrollableRegion("playlistSelectScroll", new(280, 300), gap: 2);
            {
                foreach (var playlist in _settings.Playlists)
                {
                    if (!UI.Button(playlist.Name, playlist.Name, new Vector2(280, 24), textAlignment: new Alignment(HAlignment.Left, VAlignment.Center)))
                    {
                        continue;
                    }
                    if (!playlist.Tracks.Any(t => t.FilePath == song.FilePath))
                    {
                        playlist.Tracks.Add(song);
                        SaveState();
                    }
                    _host.ModalWindowService.CloseModalWindow(0);
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