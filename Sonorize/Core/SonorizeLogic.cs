using System.Numerics;
using System.Text.Json;
using DirectUI;
using DirectUI.Backends.SkiaSharp;
using DirectUI.Core;
using DirectUI.Drawing;
using Sonorize.Audio;
using Sonorize.Services;

namespace Sonorize;

public class SonorizeLogic : IAppLogic
{
    private readonly IWindowHost _host;
    private Settings _settings;
    private readonly string _settingsFilePath = "settings.json";

    private readonly MusicLibrary _musicLibrary = new();
    private readonly PlaybackManager _playbackManager;
    private readonly UIStateService _uiState = new();
    private readonly PlaylistService _playlistService;
    private readonly AppController _appController;

    private readonly PlayerBarView _playerBarView;
    private readonly LibraryView _libraryView;
    private readonly SidebarView _sidebarView;
    private readonly MainContentView _mainContentView;
    private readonly SettingsWindow _settingsWindow;

    public SonorizeLogic(IWindowHost host)
    {
        _host = host;
        _settings = LoadState();

        ConfigureWindowStyles(host);

        _playbackManager = new PlaybackManager(new AudioPlayer());
        _playlistService = new PlaylistService(_settings, SaveState);

        _settingsWindow = new(_settings, _host, SaveState);
        _playerBarView = new PlayerBarView(_playbackManager, _settings);
        _libraryView = new LibraryView(_musicLibrary, _playbackManager, _settings, null);

        _sidebarView = new SidebarView(_settings, _uiState, null);
        _mainContentView = new MainContentView(_libraryView, _settings, _uiState, null);

        _appController = new AppController(
            _host,
            _settings,
            _uiState,
            _libraryView,
            _settingsWindow,
            _musicLibrary,
            _playlistService,
            SaveState
        );

        _libraryView.OnSongRightClicked = _appController.OnSongRightClicked;
        _sidebarView.OnCreatePlaylist = _appController.OnCreatePlaylist;
        _mainContentView.OpenSettingsCallback = _appController.OpenSettingsModal;

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
            JsonSerializerOptions options = new() { WriteIndented = true };
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

        float menuBarHeight = 0f;
        float playerBarHeight = 90f;

        var mainAreaSize = new Vector2(context.Renderer.RenderTargetSize.X, context.Renderer.RenderTargetSize.Y - menuBarHeight - playerBarHeight);

        _sidebarView.Draw(context, mainAreaSize.Y);
        _mainContentView.Draw(context, mainAreaSize);
        _playerBarView.Draw(context);

        _appController.HandleContextMenu();
    }
}