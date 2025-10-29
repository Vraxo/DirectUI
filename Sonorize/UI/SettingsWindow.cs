using DirectUI;
using DirectUI.Core;
using System.Numerics;

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
        float padding = 10;
        var scrollAreaSize = new Vector2(context.Renderer.RenderTargetSize.X, context.Renderer.RenderTargetSize.Y);

        // The entire modal content is now inside one scrollable region with internal padding.
        UI.BeginScrollableRegion("settingsScroll", scrollAreaSize, out float innerWidth, new Vector2(padding, padding));
        {
            // This VBox now starts at (0,0) relative to the padded scroll area.
            UI.BeginVBoxContainer("settingsVBox", Vector2.Zero, 15);
            {
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
                UI.Separator(innerWidth);
                UI.Text("dirsHeader", "Music Library Directories", style: new ButtonStyle { FontSize = 16 });
                DrawDirectoriesList(innerWidth);
                DrawActionButtons();

                // --- OK/Cancel Buttons ---
                UI.Separator(innerWidth);
                DrawOkCancelButtons(innerWidth);
            }
            UI.EndVBoxContainer();
        }
        UI.EndScrollableRegion();
    }

    private void DrawDirectoriesList(float width)
    {
        // Restore a reasonable height now that the whole panel is scrollable.
        UI.BeginScrollableRegion("dirsScrollInner", new Vector2(width, 140), out float innerWidth);
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
        UI.BeginHBoxContainer("dirsButtons", Vector2.Zero, 5);
        {
            if (UI.Button("addDir", "Add Folder...", new Vector2(120, 24)))
            {
                var selectedPath = FileDialogs.PickFolder("Select Music Folder");
                if (!string.IsNullOrWhiteSpace(selectedPath) && !_settings.Directories.Contains(selectedPath))
                {
                    _settings.Directories.Add(selectedPath);
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
        // Buttons are now part of the main VBox flow, positioned with an HBox.
        UI.BeginHBoxContainer("okCancelButtonsHBox", Vector2.Zero, 5);
        {
            // Spacer to push buttons to the right
            float buttonsTotalWidth = 80 + 5 + 80; // cancel + gap + ok
            float spacerWidth = width - buttonsTotalWidth;
            if (spacerWidth > 0)
            {
                UI.Context.Layout.AdvanceLayout(new Vector2(spacerWidth, 0));
            }

            if (UI.Button("cancelSettings", "Cancel", new Vector2(80, 24)))
            {
                _host.ModalWindowService.CloseModalWindow(1); // 1 indicates cancel
            }
            if (UI.Button("okSettings", "OK", new Vector2(80, 24)))
            {
                _onSave();
                _host.ModalWindowService.CloseModalWindow(0); // 0 indicates success
            }
        }
        UI.EndHBoxContainer();
    }
}