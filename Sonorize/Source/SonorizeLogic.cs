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
    private readonly Settings _settings;
    private readonly string _settingsFilePath = "settings.json";

    private readonly MenuBar _menuBar;
    private readonly SettingsWindow _settingsWindow;
    private readonly MusicLibrary _musicLibrary = new();
    private readonly PlaybackManager _playbackManager;
    private readonly PlaybackControlsView _playbackControlsView;
    private readonly LibraryView _libraryView;
    private readonly AlbumsView _albumsView;
    private readonly ArtistsView _artistsView;

    // --- Navigation State ---
    private int _activeTabIndex = 0;
    private readonly string[] _tabLabels = { "Songs", "Albums", "Artists" };
    private string? _artistFilter;
    private AlbumInfo? _albumFilter;


    public SonorizeLogic(IWindowHost host)
    {
        _host = host;
        _settings = LoadState();

        ConfigureWindowStyles(host);

        _menuBar = new(OpenSettingsModal);
        _settingsWindow = new(_settings, _host);
        _playbackManager = new PlaybackManager(new AudioPlayer());
        _playbackControlsView = new PlaybackControlsView(_playbackManager);

        // Pass callbacks to the views for drill-down navigation
        _libraryView = new LibraryView(_musicLibrary, _playbackManager, _settings);
        _albumsView = new AlbumsView(_musicLibrary, OnAlbumSelected);
        _artistsView = new ArtistsView(_musicLibrary, OnArtistSelected);

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
        finally
        {
            _playbackManager.Dispose();
        }
    }

    private static void ConfigureWindowStyles(IWindowHost host)
    {
        if (host is not SilkNetWindowHost silkHost)
        {
            return;
        }

        // ========================================================================
        // == CONFIGURING WINDOW BACKDROP AND TITLE BAR (WINDOWS 11+)           ==
        // ========================================================================
        // This example enables a modern Mica window with a dark title bar.
        // It requires Windows 11 (Build 22621 or newer). On older systems,
        // it will fall back to a standard solid color window.
        silkHost.BackdropType = WindowBackdropType.Default;
        silkHost.TitleBarTheme = WindowTitleBarTheme.Dark;
    }

    public void DrawUI(UIContext context)
    {
        _menuBar.Draw(context);

        _playbackManager.Update();

        float menuBarHeight = 30f;
        float tabBarHeight = 30f;
        float playbackControlsHeight = 70f;
        float padding = 10f;

        var tabBarPos = new Vector2(padding, menuBarHeight + padding);

        var tabTheme = new ButtonStylePack();
        tabTheme.Normal.FillColor = new DirectUI.Drawing.Color(45 / 255f, 45 / 255f, 48 / 255f, 1.0f);
        tabTheme.Normal.BorderColor = DefaultTheme.NormalBorder;
        tabTheme.Hover.FillColor = new DirectUI.Drawing.Color(60 / 255f, 60 / 255f, 63 / 255f, 1.0f);

        UI.BeginHBoxContainer("tabBarHBox", tabBarPos, 2);
        {
            // Draw "Back" button if we are in a filtered view
            if (_albumFilter != null || _artistFilter != null)
            {
                if (UI.Button("backNav", "<", new Vector2(30, tabBarHeight)))
                {
                    OnBack();
                }
            }

            UI.TabBar("mainTabs", _tabLabels, ref _activeTabIndex, tabTheme);
        }
        UI.EndHBoxContainer();

        var gridPos = new Vector2(padding, tabBarPos.Y + tabBarHeight + 2);
        var gridSize = new Vector2(
            context.Renderer.RenderTargetSize.X - (padding * 2),
            context.Renderer.RenderTargetSize.Y - gridPos.Y - playbackControlsHeight - padding
        );

        switch (_activeTabIndex)
        {
            case 0: // Songs
                _libraryView.Draw(context, gridPos, gridSize, _albumFilter);
                break;
            case 1: // Albums
                _albumsView.Draw(context, gridPos, gridSize, _artistFilter);
                break;
            case 2: // Artists
                _artistsView.Draw(context, gridPos, gridSize);
                break;
        }

        _playbackControlsView.Draw(context);
    }

    private void OnArtistSelected(string artistName)
    {
        _artistFilter = artistName;
        _albumFilter = null; // Clear album filter when selecting an artist
        _activeTabIndex = 1; // Switch to Albums tab
    }

    private void OnAlbumSelected(AlbumInfo album)
    {
        _albumFilter = album;
        // Artist filter can remain, as it's part of the hierarchy
        _activeTabIndex = 0; // Switch to Songs tab
    }

    private void OnBack()
    {
        // If we are viewing an album's songs, go back to the album list.
        if (_albumFilter != null)
        {
            _albumFilter = null;
            _activeTabIndex = 1; // Go to albums tab
        }
        // If we are viewing an artist's albums, go back to the artist list.
        else if (_artistFilter != null)
        {
            _artistFilter = null;
            _activeTabIndex = 2; // Go to artists tab
        }
    }

    private void OpenSettingsModal()
    {
        _host.ModalWindowService.OpenModalWindow("Settings", 500, 400, _settingsWindow.Draw, resultCode =>
        {
            if (resultCode == 0)
            {
                _musicLibrary.ScanDirectoriesAsync(_settings.Directories);
            }
        });
    }
}