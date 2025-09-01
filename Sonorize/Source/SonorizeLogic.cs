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
    private readonly AudioPlayer _audioPlayer;

    private int _selectedTrackIndex = -1;
    private int _previousSelectedTrackIndex = -1;
    private readonly DataGridColumn[] _columns;


    public SonorizeLogic(IWindowHost host)
    {
        _host = host;
        _settings = LoadState();

        ConfigureWindowStyles(host);

        _menuBar = new(OpenSettingsModal);
        _settingsWindow = new(_settings, _host);
        _audioPlayer = new AudioPlayer();

        _columns =
        [
            new DataGridColumn("Title", 350, nameof(MusicFile.Title)),
            new DataGridColumn("Artist", 250, nameof(MusicFile.Artist)),
            new DataGridColumn("Album", 250, nameof(MusicFile.Album)),
            new DataGridColumn("Duration", 100, nameof(MusicFile.Duration)),
            new DataGridColumn("Genre", 150, nameof(MusicFile.Genre)),
            new DataGridColumn("Year", 80, nameof(MusicFile.Year))
        ];

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
            _audioPlayer.Dispose();
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

        float menuBarHeight = 30f;
        float playbackControlsHeight = 40f; // Space for the slider
        float padding = 10f;
        var gridPos = new Vector2(padding, menuBarHeight + padding);
        var gridSize = new Vector2(
            context.Renderer.RenderTargetSize.X - (padding * 2),
            context.Renderer.RenderTargetSize.Y - menuBarHeight - playbackControlsHeight - (padding * 2)
        );

        if (gridSize.X > 0 && gridSize.Y > 0)
        {
            UI.DataGrid<MusicFile>(
                "musicGrid",
                _musicLibrary.Files,
                _columns,
                ref _selectedTrackIndex,
                gridSize,
                gridPos,
                autoSizeColumns: true,
                trimCellText: true
            );
        }

        // Check for selection change to play music
        if (_selectedTrackIndex != _previousSelectedTrackIndex)
        {
            if (_selectedTrackIndex >= 0 && _selectedTrackIndex < _musicLibrary.Files.Count)
            {
                var trackToPlay = _musicLibrary.Files[_selectedTrackIndex];
                _audioPlayer.Play(trackToPlay.FilePath);
            }
            _previousSelectedTrackIndex = _selectedTrackIndex;
        }

        DrawPlaybackControls(context);
    }

    private void DrawPlaybackControls(UIContext context)
    {
        float playbackControlsHeight = 40f;
        float padding = 10f;
        var windowSize = context.Renderer.RenderTargetSize;

        var controlsY = windowSize.Y - playbackControlsHeight;
        var controlsRect = new Vortice.Mathematics.Rect(0, controlsY, windowSize.X, playbackControlsHeight);

        // Draw a background for the controls area
        var bgStyle = new BoxStyle
        {
            FillColor = new(0.1f, 0.1f, 0.1f, 1.0f),
            BorderColor = new(0.05f, 0.05f, 0.05f, 1.0f),
            BorderLengthTop = 1f,
            BorderLengthBottom = 0,
            BorderLengthLeft = 0,
            BorderLengthRight = 0,
            Roundness = 0
        };
        context.Renderer.DrawBox(controlsRect, bgStyle);

        // Get audio state
        double currentPosition = _audioPlayer.GetPosition();
        double totalDuration = _audioPlayer.GetLength();
        bool isAudioLoaded = totalDuration > 0;

        // Use a container to position the slider
        var sliderAreaHeight = 20f;
        var sliderAreaY = controlsY + (playbackControlsHeight - sliderAreaHeight) / 2f;
        UI.BeginVBoxContainer("playbackControls", new Vector2(padding, sliderAreaY));
        {
            var sliderWidth = windowSize.X - (padding * 2);
            var sliderSize = new Vector2(sliderWidth, sliderAreaHeight);

            float sliderValue = (float)currentPosition;

            float newSliderValue = UI.HSlider(
                id: "seekSlider",
                currentValue: sliderValue,
                minValue: 0f,
                maxValue: isAudioLoaded ? (float)totalDuration : 1.0f,
                size: sliderSize,
                disabled: !isAudioLoaded
            );

            // If user dragged the slider (and audio is loaded), seek to new position.
            // Check for a meaningful change to avoid seeking due to float precision issues.
            if (isAudioLoaded && Math.Abs(newSliderValue - sliderValue) > 0.01f)
            {
                _audioPlayer.Seek(newSliderValue);
            }
        }
        UI.EndVBoxContainer();
    }

    private void OpenSettingsModal()
    {
        _host.ModalWindowService.OpenModalWindow("Settings", 500, 400, _settingsWindow.Draw, resultCode =>
        {
            // After settings window is closed, rescan library if OK was pressed.
            if (resultCode == 0)
            {
                _musicLibrary.ScanDirectoriesAsync(_settings.Directories);
            }
        });
    }
}