using DirectUI;
using DirectUI.Core;
using System.Numerics;
using TinyDialogsNet;

namespace Sonorize;

public class SettingsWindow
{
    private readonly Settings _settings;
    private readonly IWindowHost _host;
    private readonly Action _onSave;
    private int _selectedDirectoryIndex = -1;

    public SettingsWindow(Settings settings, IWindowHost host, Action onSave)
    {
        _settings = settings;
        _host = host;
        _onSave = onSave;
    }

    public void Draw(UIContext context)
    {
        float contentWidth = 480;
        UI.BeginVBoxContainer("settingsVBox", new Vector2(10, 10), 15);

        // --- Appearance Section ---
        UI.Text("appearanceHeader", "Appearance", style: new ButtonStyle { FontSize = 16 });

        bool zebraStriping = _settings.EnableZebraStriping;
        if (UI.Checkbox("zebraStriping", "Enable Zebra Striping", ref zebraStriping))
        {
            _settings.EnableZebraStriping = zebraStriping;
        }

        UI.Text("compactnessLabel", "Compactness");
        float compactness = _settings.CompactnessLevel;
        float newCompactness = UI.HSlider("compactnessSlider", compactness, 1, 3, new Vector2(150, 20), step: 1);
        if (newCompactness != compactness)
        {
            _settings.CompactnessLevel = (int)Math.Round(newCompactness);
        }


        UI.Text("overflowLabel", "Track List Text Overflow");
        int overflowIndex = (int)_settings.TrackListOverflow;
        if (UI.RadioButtons("overflowRadio", ["Wrap", "Truncate"], ref overflowIndex, size: new Vector2(100, 24)))
        {
            _settings.TrackListOverflow = (TextOverflowMode)overflowIndex;
        }


        // --- Library Section ---
        UI.Separator(contentWidth);
        UI.Text("dirsHeader", "Music Library Directories", style: new ButtonStyle { FontSize = 16 });
        DrawDirectoriesList(contentWidth);
        DrawActionButtons();
        DrawOkCancelButtons(contentWidth);

        UI.EndVBoxContainer();
    }

    private void DrawDirectoriesList(float width)
    {
        UI.BeginScrollableRegion("dirsScroll", new Vector2(width, 140), out float innerWidth);
        {
            var selectedStyle = new ButtonStylePack();
            selectedStyle.Active.FillColor = DefaultTheme.Accent;
            selectedStyle.ActiveHover.FillColor = DefaultTheme.Accent;
            selectedStyle.Normal.FillColor = new(0.15f, 0.15f, 0.15f, 1.0f);

            for (int i = 0; i < _settings.Directories.Count; i++)
            {
                bool isSelected = i == _selectedDirectoryIndex;
                if (UI.Button($"dir_{i}", _settings.Directories[i], new Vector2(innerWidth, 24), theme: selectedStyle,
                    textAlignment: new Alignment(HAlignment.Left, VAlignment.Center), textMargin: new Vector2(5, 0), isActive: isSelected))
                {
                    _selectedDirectoryIndex = i;
                }
            }
        }
        UI.EndScrollableRegion();
    }

    private void DrawActionButtons()
    {
        UI.BeginHBoxContainer("dirsButtons", UI.Context.Layout.GetCurrentPosition(), 5);
        {
            if (UI.Button("addDir", "Add Folder...", new Vector2(120, 24)))
            {
                var result = TinyDialogs.SelectFolderDialog("Select Music Folder");
                if (!string.IsNullOrWhiteSpace(result.Path) && !_settings.Directories.Contains(result.Path))
                {
                    _settings.Directories.Add(result.Path);
                }
            }
            if (UI.Button("removeDir", "Remove", new Vector2(80, 24), disabled: _selectedDirectoryIndex < 0))
            {
                if (_selectedDirectoryIndex >= 0 && _selectedDirectoryIndex < _settings.Directories.Count)
                {
                    _settings.Directories.RemoveAt(_selectedDirectoryIndex);
                    _selectedDirectoryIndex = -1;
                }
            }
        }
        UI.EndHBoxContainer();
    }

    private void DrawOkCancelButtons(float width)
    {
        float modalHeight = 400;
        float buttonY = modalHeight - 24 - 10;
        float okButtonX = width - 80;
        float cancelButtonX = okButtonX - 80 - 5;

        // Use manual positioning for buttons at the bottom of the modal
        UI.BeginVBoxContainer("okButtonContainer", new Vector2(okButtonX, buttonY));
        if (UI.Button("okSettings", "OK", new Vector2(80, 24)))
        {
            _onSave();
            _host.ModalWindowService.CloseModalWindow(0);
        }
        UI.EndVBoxContainer();

        UI.BeginVBoxContainer("cancelButtonContainer", new Vector2(cancelButtonX, buttonY));
        if (UI.Button("cancelSettings", "Cancel", new Vector2(80, 24)))
        {
            _host.ModalWindowService.CloseModalWindow(1);
        }
        UI.EndVBoxContainer();
    }
}