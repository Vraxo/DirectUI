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

    private int _selectedTrackIndex = -1;

    private readonly DataGridColumn[] _columns;

    public SonorizeLogic(IWindowHost host)
    {
        _host = host;
        _settings = LoadState();

        ConfigureWindowStyles(host);

        _menuBar = new(OpenSettingsModal);
        _settingsWindow = new(_settings, _host);
        _playbackManager = new PlaybackManager(new AudioPlayer());

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

        // Update playback manager every frame. This checks for song completion.
        _playbackManager.Update();
        // Keep the playback manager's tracklist in sync with the library.
        _playbackManager.SetTracklist(_musicLibrary.Files);

        float menuBarHeight = 30f;
        float playbackControlsHeight = 70f;
        float padding = 10f;
        var gridPos = new Vector2(padding, menuBarHeight + padding);
        var gridSize = new Vector2(
            context.Renderer.RenderTargetSize.X - (padding * 2),
            context.Renderer.RenderTargetSize.Y - menuBarHeight - playbackControlsHeight - (padding * 2)
        );

        int previousSelectedTrackIndex = _selectedTrackIndex;
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

        // Sync state between UI selection and playback manager
        if (_selectedTrackIndex != previousSelectedTrackIndex)
        {
            // User selected a new track in the grid: tell the manager to play it.
            _playbackManager.Play(_selectedTrackIndex);
        }
        else if (_selectedTrackIndex != _playbackManager.CurrentTrackIndex)
        {
            // Playback manager changed the track (e.g., next song): update grid selection.
            _selectedTrackIndex = _playbackManager.CurrentTrackIndex;
        }

        DrawPlaybackControls(context);
    }

    private void DrawPlaybackControls(UIContext context)
    {
        float playbackControlsHeight = 70f; // Increased for buttons
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

        // Get audio state from the playback manager
        double currentPosition = _playbackManager.CurrentPosition;
        double totalDuration = _playbackManager.TotalDuration;
        bool isAudioLoaded = totalDuration > 0;
        bool isAnyTrackAvailable = _musicLibrary.Files.Any();

        // Use a container to layout slider and buttons
        UI.BeginVBoxContainer("playbackVBox", new Vector2(padding, controlsY + 5), 5);
        {
            // --- Slider ---
            var sliderWidth = windowSize.X - (padding * 2);
            var sliderSize = new Vector2(sliderWidth, 10);
            float sliderValue = (float)currentPosition;

            float newSliderValue = UI.HSlider(
                id: "seekSlider",
                currentValue: sliderValue,
                minValue: 0f,
                maxValue: isAudioLoaded ? (float)totalDuration : 1.0f,
                size: sliderSize,
                disabled: !isAudioLoaded,
                grabberSize: new Vector2(10, 20)
            );

            if (isAudioLoaded && Math.Abs(newSliderValue - sliderValue) > 0.01f)
            {
                _playbackManager.Seek(newSliderValue);
            }

            // --- Buttons ---
            var buttonSize = new Vector2(35, 24); // Make buttons more compact for icons
            var totalButtonsWidth = (buttonSize.X * 5) + (5 * 4); // 5 buttons, 4 gaps
            var buttonsStartX = (windowSize.X - totalButtonsWidth) / 2;
            var buttonsY = UI.Context.Layout.GetCurrentPosition().Y + 5; // Add some padding from slider
            const int controlsLayer = 10;

            var iconButtonTheme = new ButtonStylePack { FontSize = 16f };
            var toggleButtonTheme = new ButtonStylePack { FontSize = 16f };
            toggleButtonTheme.Active.FillColor = DefaultTheme.Accent;
            toggleButtonTheme.Active.BorderColor = DefaultTheme.AccentBorder;
            toggleButtonTheme.ActiveHover.FillColor = DefaultTheme.Accent;
            toggleButtonTheme.ActiveHover.BorderColor = DefaultTheme.AccentBorder;


            UI.BeginHBoxContainer("playbackButtons", new Vector2(buttonsStartX, buttonsY), 5);
            {
                bool isShuffleActive = _playbackManager.Mode == PlaybackMode.Shuffle;
                if (UI.Button("shuffle", "🔀", buttonSize, theme: toggleButtonTheme, disabled: !isAnyTrackAvailable, layer: controlsLayer, isActive: isShuffleActive))
                {
                    _playbackManager.Mode = isShuffleActive ? PlaybackMode.Sequential : PlaybackMode.Shuffle;
                }

                if (UI.Button("prevTrack", "⏮", buttonSize, theme: iconButtonTheme, disabled: !isAnyTrackAvailable, layer: controlsLayer))
                {
                    _playbackManager.Previous();
                }

                string playPauseText = _playbackManager.IsPlaying ? "⏸" : "▶";
                if (UI.Button("playPause", playPauseText, buttonSize, theme: iconButtonTheme, disabled: !isAnyTrackAvailable, layer: controlsLayer))
                {
                    _playbackManager.TogglePlayPause();
                }

                if (UI.Button("nextTrack", "⏭", buttonSize, theme: iconButtonTheme, disabled: !isAnyTrackAvailable, layer: controlsLayer))
                {
                    _playbackManager.Next();
                }

                string repeatText = _playbackManager.EndAction switch
                {
                    PlaybackEndAction.NextInQueue => "🔁",
                    PlaybackEndAction.RepeatSong => "🔂",
                    PlaybackEndAction.DoNothing => "➡",
                    _ => "?"
                };
                if (UI.Button("repeatMode", repeatText, buttonSize, theme: iconButtonTheme, disabled: !isAnyTrackAvailable, layer: controlsLayer))
                {
                    _playbackManager.EndAction = (PlaybackEndAction)(((int)_playbackManager.EndAction + 1) % 3);
                }
            }
            UI.EndHBoxContainer();
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