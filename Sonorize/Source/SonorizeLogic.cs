using System.Numerics;
using System.Text.Json;
using DirectUI;
using DirectUI.Backends.SkiaSharp;
using DirectUI.Core;
using ManagedBass;
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
    private string? _currentlyPlayingFilePath;
    private readonly DataGridColumn[] _columns;

    private enum PlaybackEndAction { NextInQueue, RepeatSong, DoNothing }
    private PlaybackEndAction _playbackEndAction = PlaybackEndAction.NextInQueue;
    private bool _isShuffleActive = false;
    private readonly Random _random = new();
    private PlaybackState _lastPlaybackState = PlaybackState.Stopped;


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
        float playbackControlsHeight = 70f; // Increased for buttons
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

        UpdatePlaybackState();

        // Check for selection change to play music
        if (_selectedTrackIndex != _previousSelectedTrackIndex)
        {
            if (_selectedTrackIndex >= 0 && _selectedTrackIndex < _musicLibrary.Files.Count)
            {
                var trackToPlay = _musicLibrary.Files[_selectedTrackIndex];
                // Only play if the file path is different from the currently playing song.
                // This prevents restarting the song when sorting the grid or when auto-advancing.
                if (trackToPlay.FilePath != _currentlyPlayingFilePath)
                {
                    _audioPlayer.Play(trackToPlay.FilePath);
                    _currentlyPlayingFilePath = trackToPlay.FilePath;
                    _lastPlaybackState = _audioPlayer.CurrentState;
                }
            }
            else
            {
                // If selection is cleared (index is -1), stop playback.
                _audioPlayer.Stop();
                _currentlyPlayingFilePath = null;
                _lastPlaybackState = PlaybackState.Stopped;
            }
            _previousSelectedTrackIndex = _selectedTrackIndex;
        }


        DrawPlaybackControls(context);
    }

    private void UpdatePlaybackState()
    {
        var currentState = _audioPlayer.CurrentState;
        // Check if the song just finished playing
        if (_lastPlaybackState == PlaybackState.Playing && currentState == PlaybackState.Stopped)
        {
            HandleSongFinished();
        }
        _lastPlaybackState = currentState;
    }

    private void HandleSongFinished()
    {
        if (_musicLibrary.Files.Count == 0 || _selectedTrackIndex < 0)
        {
            _currentlyPlayingFilePath = null;
            _lastPlaybackState = PlaybackState.Stopped;
            return;
        }

        int nextIndex = -1;

        switch (_playbackEndAction)
        {
            case PlaybackEndAction.DoNothing:
                _currentlyPlayingFilePath = null;
                _lastPlaybackState = PlaybackState.Stopped;
                _selectedTrackIndex = -1; // Deselect track
                _previousSelectedTrackIndex = -1;
                return;

            case PlaybackEndAction.RepeatSong:
                nextIndex = _selectedTrackIndex;
                break;

            case PlaybackEndAction.NextInQueue:
                if (_isShuffleActive)
                {
                    if (_musicLibrary.Files.Count <= 1)
                    {
                        nextIndex = 0;
                    }
                    else
                    {
                        do
                        {
                            nextIndex = _random.Next(_musicLibrary.Files.Count);
                        } while (nextIndex == _selectedTrackIndex);
                    }
                }
                else
                {
                    nextIndex = _selectedTrackIndex + 1;
                    if (nextIndex >= _musicLibrary.Files.Count)
                    {
                        nextIndex = 0; // Wrap around
                    }
                }
                break;
        }

        if (nextIndex != -1)
        {
            _selectedTrackIndex = nextIndex;
            _previousSelectedTrackIndex = nextIndex; // Important to prevent re-triggering playback from main loop
            var trackToPlay = _musicLibrary.Files[_selectedTrackIndex];
            _audioPlayer.Play(trackToPlay.FilePath);
            _currentlyPlayingFilePath = trackToPlay.FilePath;
            _lastPlaybackState = _audioPlayer.CurrentState;
        }
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

        // Get audio state
        double currentPosition = _audioPlayer.GetPosition();
        double totalDuration = _audioPlayer.GetLength();
        bool isAudioLoaded = totalDuration > 0;
        bool isTrackSelected = _selectedTrackIndex >= 0;

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
                _audioPlayer.Seek(newSliderValue);
            }

            // --- Buttons ---
            var buttonSize = new Vector2(35, 24); // Make buttons more compact for icons
            var totalButtonsWidth = (buttonSize.X * 5) + (5 * 4); // 5 buttons, 4 gaps
            var buttonsStartX = (windowSize.X - totalButtonsWidth) / 2;
            var buttonsY = UI.Context.Layout.GetCurrentPosition().Y + 5; // Add some padding from slider
            const int controlsLayer = 10; // Use a higher layer for controls

            var iconButtonTheme = new ButtonStylePack { FontSize = 16f };
            var toggleButtonTheme = new ButtonStylePack { FontSize = 16f };
            toggleButtonTheme.Active.FillColor = DefaultTheme.Accent;
            toggleButtonTheme.Active.BorderColor = DefaultTheme.AccentBorder;
            toggleButtonTheme.ActiveHover.FillColor = DefaultTheme.Accent;
            toggleButtonTheme.ActiveHover.BorderColor = DefaultTheme.AccentBorder;


            UI.BeginHBoxContainer("playbackButtons", new Vector2(buttonsStartX, buttonsY), 5);
            {
                if (UI.Button("shuffle", "🔀", buttonSize, theme: toggleButtonTheme, disabled: !isTrackSelected, layer: controlsLayer, isActive: _isShuffleActive))
                {
                    _isShuffleActive = !_isShuffleActive;
                }

                if (UI.Button("prevTrack", "⏮", buttonSize, theme: iconButtonTheme, disabled: !isTrackSelected, layer: controlsLayer))
                {
                    if (_musicLibrary.Files.Count > 0)
                    {
                        _selectedTrackIndex--;
                        if (_selectedTrackIndex < 0)
                        {
                            _selectedTrackIndex = _musicLibrary.Files.Count - 1;
                        }
                    }
                }

                string playPauseText = _audioPlayer.IsPlaying ? "⏸" : "▶";
                if (UI.Button("playPause", playPauseText, buttonSize, theme: iconButtonTheme, disabled: !isTrackSelected, layer: controlsLayer))
                {
                    if (_audioPlayer.IsPlaying)
                    {
                        _audioPlayer.Pause();
                    }
                    else
                    {
                        if (isAudioLoaded && _audioPlayer.CurrentState == PlaybackState.Paused)
                        {
                            _audioPlayer.Resume();
                        }
                        else if (isTrackSelected)
                        {
                            // This will trigger the selection changed logic to play the track
                            _previousSelectedTrackIndex = -1;
                        }
                    }
                }

                if (UI.Button("nextTrack", "⏭", buttonSize, theme: iconButtonTheme, disabled: !isTrackSelected, layer: controlsLayer))
                {
                    if (_musicLibrary.Files.Count > 0)
                    {
                        if (_isShuffleActive)
                        {
                            if (_musicLibrary.Files.Count > 1)
                            {
                                int current = _selectedTrackIndex;
                                int next;
                                do { next = _random.Next(_musicLibrary.Files.Count); } while (next == current);
                                _selectedTrackIndex = next;
                            }
                            else
                            {
                                _selectedTrackIndex = 0;
                            }
                        }
                        else
                        {
                            _selectedTrackIndex = (_selectedTrackIndex + 1) % _musicLibrary.Files.Count;
                        }
                    }
                }

                string repeatText = _playbackEndAction switch
                {
                    PlaybackEndAction.NextInQueue => "🔁",
                    PlaybackEndAction.RepeatSong => "🔂",
                    PlaybackEndAction.DoNothing => "➡",
                    _ => "?"
                };
                if (UI.Button("repeatMode", repeatText, buttonSize, theme: iconButtonTheme, disabled: !isTrackSelected, layer: controlsLayer))
                {
                    _playbackEndAction = (PlaybackEndAction)(((int)_playbackEndAction + 1) % 3);
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