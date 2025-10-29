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

        // The entire modal content is now inside one scrollable region.
        // We pass padding and the gap between elements directly to it.
        UI.BeginScrollableRegion("settingsScroll", scrollAreaSize, gap: 15, padding: new Vector2(padding, padding));
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
            UI.Separator();
            UI.Text("dirsHeader", "Music Library Directories", style: new ButtonStyle { FontSize = 16 });
            DrawDirectoriesList();
            DrawActionButtons();

            // --- OK/Cancel Buttons ---
            UI.Separator();
            DrawOkCancelButtons();
        }
        UI.EndScrollableRegion();
    }

    private void DrawDirectoriesList()
    {
        float width = UI.GetAvailableContentWidth();
        UI.BeginScrollableRegion("dirsScrollInner", new Vector2(width, 140), gap: 2);
        {
            float innerWidth = UI.GetAvailableContentWidth();
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

    private void DrawOkCancelButtons()
    {
        float width = UI.GetAvailableContentWidth();
        UI.BeginHBoxContainer("okCancelButtonsHBox", Vector2.Zero, 5);
        {
            float buttonGap = 5;
            float okButtonWidth = 80;
            float cancelButtonWidth = 80;

            // Total width consumed by visible elements and the gap BETWEEN them.
            float buttonsAndGapsWidth = cancelButtonWidth + buttonGap + okButtonWidth;

            // The HBox will add a gap *after* our spacer element, so we must account for it.
            float spacerWidth = width - buttonsAndGapsWidth - buttonGap;

            if (spacerWidth > 0)
            {
                UI.Context.Layout.AdvanceLayout(new Vector2(spacerWidth, 0));
            }

            if (UI.Button("cancelSettings", "Cancel", new Vector2(cancelButtonWidth, 24)))
            {
                _host.ModalWindowService.CloseModalWindow(1); // 1 indicates cancel
            }
            if (UI.Button("okSettings", "OK", new Vector2(okButtonWidth, 24)))
            {
                _onSave();
                _host.ModalWindowService.CloseModalWindow(0); // 0 indicates success
            }
        }
        UI.EndHBoxContainer();
    }
}