using System.Numerics;
using DirectUI;
using DirectUI.Drawing;
using Sonorize.Audio;

namespace Sonorize;

public class PlaybackControlsView
{
    private readonly PlaybackManager _playbackManager;

    public PlaybackControlsView(PlaybackManager playbackManager)
    {
        _playbackManager = playbackManager;
    }

    public void Draw(UIContext context, float panelWidth)
    {
        Vector2 smallButtonSize = new(32, 32);
        Vector2 playButtonSize = new(36, 36);

        ButtonStylePack theme = new()
        {
            FontName = "FontAwesomeSolid",
            FontSize = 14,
            Roundness = 1f,
            Normal = new()
            {
                FillColor = Colors.Transparent,
            }
        };

        ButtonStylePack toggleTheme = new(theme);
        toggleTheme.Active.FontColor = DefaultTheme.Accent;

        ButtonStylePack playTheme = new(theme) { FontSize = 22 };

        float gap = 16f;
        float buttonsWidth = (smallButtonSize.X * 4) + playButtonSize.X + (gap * 4);
        float buttonsStartX = (panelWidth - buttonsWidth) / 2;

        UI.BeginHBoxContainer("playbackButtons", new Vector2(buttonsStartX, 0), gap, VAlignment.Center, fixedRowHeight: playButtonSize.Y);
        {
            DrawShuffleButton(smallButtonSize, toggleTheme);
            DrawPreviousButton(smallButtonSize, theme);
            DrawPlayButton(playButtonSize, playTheme);
            DrawNextButton(smallButtonSize, theme);
            DrawRepeatButton(smallButtonSize, toggleTheme);
        }

        UI.EndHBoxContainer();
    }

    private void DrawRepeatButton(Vector2 smallButtonSize, ButtonStylePack toggleTheme)
    {
        bool isRepeatActive = _playbackManager.EndAction == PlaybackEndAction.RepeatSong;

        if (!UI.Button("repeat", "\uF363", smallButtonSize, theme: toggleTheme, isActive: isRepeatActive))
        {
            return;
        }

        _playbackManager.EndAction = isRepeatActive
            ? PlaybackEndAction.NextInQueue
            : PlaybackEndAction.RepeatSong;
    }

    private void DrawNextButton(Vector2 smallButtonSize, ButtonStylePack theme)
    {
        if (!UI.Button("next", "\uF051", smallButtonSize, theme: theme))
        {
            return;
        }

        _playbackManager.Next();
    }

    private void DrawPlayButton(Vector2 playButtonSize, ButtonStylePack playTheme)
    {
        string playIcon = _playbackManager.IsPlaying
            ? "\uF04C"
            : "\uF04B";

        if (!UI.Button("play", playIcon, playButtonSize, theme: playTheme))
        {
            return;
        }

        _playbackManager.TogglePlayPause();
    }

    private void DrawPreviousButton(Vector2 smallButtonSize, ButtonStylePack theme)
    {
        if (!UI.Button("prev", "\uF048", smallButtonSize, theme: theme))
        {
            return;
        }

        _playbackManager.Previous();
    }

    private void DrawShuffleButton(Vector2 smallButtonSize, ButtonStylePack toggleTheme)
    {
        bool isShuffleActive = _playbackManager.Mode == PlaybackMode.Shuffle;

        if (!UI.Button("shuffle", "\uF074", smallButtonSize, theme: toggleTheme, isActive: isShuffleActive))
        {
            return;
        }

        _playbackManager.Mode = isShuffleActive ? PlaybackMode.Sequential : PlaybackMode.Shuffle;
    }
}