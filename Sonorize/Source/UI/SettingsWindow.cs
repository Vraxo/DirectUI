using DirectUI;
using DirectUI.Core;
using System.Numerics;
using TinyDialogsNet;

namespace Sonorize;

public class SettingsWindow
{
    private readonly Settings _settings;
    private readonly IWindowHost _host;
    private int _selectedDirectoryIndex = -1;

    public SettingsWindow(Settings settings, IWindowHost host)
    {
        _settings = settings;
        _host = host;
    }

    public void Draw(UIContext context)
    {
        UI.BeginVBoxContainer("settingsVBox", new Vector2(10, 10), 10);

        UI.Text("settingsTitle", "Settings", style: new ButtonStyle { FontSize = 18 });
        UI.Separator(480, verticalPadding: 2);

        UI.Text("playbackHeader", "Playback");
        bool playOnDoubleClick = _settings.PlayOnDoubleClick;
        UI.Checkbox("playOnDoubleClick", "Play track on double-click", ref playOnDoubleClick);
        _settings.PlayOnDoubleClick = playOnDoubleClick;
        UI.Separator(480, verticalPadding: 2);

        UI.Text("dirsHeader", "Music Library Directories");

        DrawDirectoriesList();
        DrawActionButtons();
        DrawOkButton();

        UI.EndVBoxContainer();
    }

    private void DrawDirectoriesList()
    {
        UI.BeginScrollableRegion("dirsScroll", new Vector2(480, 160), out float innerWidth);
        {
            ButtonStylePack selectedStyle = new();
            selectedStyle.Active.FillColor = DefaultTheme.Accent;
            selectedStyle.ActiveHover.FillColor = DefaultTheme.Accent;
            selectedStyle.Normal.FillColor = new(0.15f, 0.15f, 0.15f, 1.0f);

            for (int i = 0; i < _settings.Directories.Count; i++)
            {
                bool isSelected = i == _selectedDirectoryIndex;

                if (!UI.Button($"dir_{i}", _settings.Directories[i], new Vector2(innerWidth, 24), theme: selectedStyle, textAlignment: new Alignment(HAlignment.Left, VAlignment.Center), textMargin: new Vector2(5, 0), isActive: isSelected))
                {
                    continue;
                }

                _selectedDirectoryIndex = i;
            }
        }

        UI.EndScrollableRegion();
    }

    private void DrawActionButtons()
    {
        UI.BeginHBoxContainer("dirsButtons", UI.Context.Layout.GetCurrentPosition(), 5);
        {
            if (UI.Button("addDir", "Add", new Vector2(80, 24)))
            {
                var selectedPath = TinyDialogs.SelectFolderDialog("Select a folder to add to Sonorize");

                if (!string.IsNullOrWhiteSpace(selectedPath.Path) && !_settings.Directories.Contains(selectedPath.Path))
                {
                    _settings.Directories.Add(selectedPath.Path);
                }
            }
            if (UI.Button("removeDir", "Remove", new Vector2(80, 24), disabled: _selectedDirectoryIndex < 0))
            {
                if (_selectedDirectoryIndex >= 0 && _selectedDirectoryIndex < _settings.Directories.Count)
                {
                    _settings.Directories.RemoveAt(_selectedDirectoryIndex);
                    _selectedDirectoryIndex = -1; // Deselect
                }
            }
        }
        UI.EndHBoxContainer();
    }

    private void DrawOkButton()
    {
        // Position at the bottom right.
        float modalHeight = 400;
        float modalWidth = 500;
        float buttonHeight = 24;
        float buttonWidth = 80;
        float padding = 10;

        float buttonY = modalHeight - buttonHeight - padding;
        float buttonX = modalWidth - buttonWidth - padding;

        UI.BeginVBoxContainer("okButtonContainer", new Vector2(buttonX, buttonY));
        if (UI.Button("okSettings", "OK", new Vector2(buttonWidth, buttonHeight)))
        {
            _host.ModalWindowService.CloseModalWindow(0);
        }
        UI.EndVBoxContainer();
    }
}