using DirectUI;
using DirectUI.Core;
using System.IO;
using System.Numerics;
using System.Text.Json;
using System;
using DirectUI.Backends.SkiaSharp; // Added for SilkNetWindowHost

namespace Sonorize;

public class SonorizeLogic : IAppLogic
{
    private readonly IWindowHost _host;
    private Settings _settings;
    private readonly string _settingsFilePath = "settings.json";

    // UI State
    private int _selectedDirectoryIndex = -1;
    private bool _isFileMenuOpen = false;
    private int _fileMenuPopupId;

    public SonorizeLogic(IWindowHost host)
    {
        _host = host;

        // Enable modern window styles if using the SkiaSharp/Silk.NET backend
        if (_host is SilkNetWindowHost silkHost)
        {
            silkHost.BackdropType = WindowBackdropType.Mica;
            silkHost.UseDarkMode = true;
        }

        _fileMenuPopupId = "fileMenuPopup".GetHashCode();
        LoadState();
    }

    private void LoadState()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                string json = File.ReadAllText(_settingsFilePath);
                _settings = JsonSerializer.Deserialize<Settings>(json) ?? new Settings();
            }
            else
            {
                _settings = new Settings();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading settings: {ex.Message}");
            _settings = new Settings();
        }
    }

    public void SaveState()
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(_settings, options);
            File.WriteAllText(_settingsFilePath, json);
            Console.WriteLine($"Settings saved to {_settingsFilePath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving settings: {ex.Message}");
        }
    }

    public void DrawUI(UIContext context)
    {
        DrawMenuBar(context);

        // Main application content would go here
        UI.BeginVBoxContainer("mainContentBox", new Vector2(20, 50));
        UI.Text("mainContent", "Main Application View", new Vector2(200, 30));
        UI.EndVBoxContainer();
    }

    private void DrawMenuBar(UIContext context)
    {
        var menuBarBg = new BoxStyle
        {
            FillColor = new(0.1f, 0.1f, 0.1f, 1.0f),
            BorderColor = new(0.05f, 0.05f, 0.05f, 1.0f),
            BorderLengthBottom = 1f,
            BorderLengthTop = 0,
            BorderLengthLeft = 0,
            BorderLengthRight = 0,
            Roundness = 0
        };
        UI.Context.Renderer.DrawBox(new Vortice.Mathematics.Rect(0, 0, UI.Context.Renderer.RenderTargetSize.X, 30), menuBarBg);

        UI.BeginHBoxContainer("menuBar", new Vector2(5, 4), 5);

        var fileButtonPos = UI.Context.Layout.GetCurrentPosition();
        var fileButtonSize = new Vector2(50, 22);

        if (_isFileMenuOpen && UI.State.ActivePopupId != _fileMenuPopupId)
        {
            _isFileMenuOpen = false;
        }

        if (UI.Button("fileMenu", "File", fileButtonSize, isActive: _isFileMenuOpen))
        {
            _isFileMenuOpen = !_isFileMenuOpen;
            if (_isFileMenuOpen)
            {
                var popupPosition = new Vector2(fileButtonPos.X, fileButtonPos.Y + fileButtonSize.Y + 2);
                var popupSize = new Vector2(150, 30);
                var popupBounds = new Vortice.Mathematics.Rect(popupPosition.X, popupPosition.Y, popupSize.X, popupSize.Y);

                Action<UIContext> drawCallback = (ctx) =>
                {
                    var popupStyle = new BoxStyle { FillColor = DefaultTheme.NormalFill, BorderColor = DefaultTheme.FocusBorder, BorderLength = 1f, Roundness = 0.1f };
                    ctx.Renderer.DrawBox(popupBounds, popupStyle);

                    var itemTheme = new ButtonStylePack { Roundness = 0f, BorderLength = 0f };
                    itemTheme.Normal.FillColor = DefaultTheme.Transparent;
                    itemTheme.Hover.FillColor = DefaultTheme.HoverFill;
                    itemTheme.Pressed.FillColor = DefaultTheme.Accent;

                    // Use a regular UI.Button inside the popup's layout context
                    UI.BeginVBoxContainer("popupContent", popupBounds.TopLeft);
                    if (UI.Button("settingsBtn", "Settings", new Vector2(popupBounds.Width, popupBounds.Height), itemTheme, textAlignment: new Alignment(HAlignment.Left, VAlignment.Center), textMargin: new Vector2(5, 0)))
                    {
                        UI.State.ClearActivePopup();
                        _isFileMenuOpen = false;
                        OpenSettingsModal();
                    }
                    UI.EndVBoxContainer();
                };
                UI.State.SetActivePopup(_fileMenuPopupId, drawCallback, popupBounds);
            }
            else
            {
                UI.State.ClearActivePopup();
            }
        }
        UI.EndHBoxContainer();
    }

    private void OpenSettingsModal()
    {
        _host.ModalWindowService.OpenModalWindow("Settings", 500, 400, DrawSettingsModal);
    }

    private void DrawSettingsModal(UIContext context)
    {
        UI.BeginVBoxContainer("settingsVBox", new Vector2(10, 10), 10);

        UI.Text("settingsTitle", "Settings", style: new ButtonStyle { FontSize = 18 });
        UI.Separator(480, verticalPadding: 2);

        UI.Text("dirsHeader", "Directories");

        // Directories List
        UI.BeginScrollableRegion("dirsScroll", new Vector2(480, 200), out float innerWidth);
        {
            var selectedStyle = new ButtonStylePack();
            selectedStyle.Active.FillColor = DefaultTheme.Accent;
            selectedStyle.ActiveHover.FillColor = DefaultTheme.Accent;
            selectedStyle.Normal.FillColor = new(0.15f, 0.15f, 0.15f, 1.0f);

            for (int i = 0; i < _settings.Directories.Count; i++)
            {
                bool isSelected = i == _selectedDirectoryIndex;
                if (UI.Button($"dir_{i}", _settings.Directories[i], new Vector2(innerWidth, 24), theme: selectedStyle, textAlignment: new Alignment(HAlignment.Left, VAlignment.Center), textMargin: new Vector2(5, 0), isActive: isSelected))
                {
                    _selectedDirectoryIndex = i;
                }
            }
        }
        UI.EndScrollableRegion();

        // Add/Remove Buttons
        UI.BeginHBoxContainer("dirsButtons", UI.Context.Layout.GetCurrentPosition(), 5);
        {
            if (UI.Button("addDir", "Add", new Vector2(80, 24)))
            {
                // In a real app, this would open a folder browser dialog.
                _settings.Directories.Add($"New Directory {_settings.Directories.Count + 1}");
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

        // OK Button - Positioned using layout containers
        // This is a bit of a hack to position at the bottom right.
        // A more robust layout system would handle this better.
        float remainingY = 400 - UI.Context.Layout.GetCurrentPosition().Y;
        float buttonY = UI.Context.Layout.GetCurrentPosition().Y + remainingY - 24 - 10;
        float buttonX = 500 - 80 - 10;

        UI.BeginVBoxContainer("okButtonContainer", new Vector2(buttonX, buttonY));
        if (UI.Button("okSettings", "OK", new Vector2(80, 24)))
        {
            _host.ModalWindowService.CloseModalWindow(0);
        }
        UI.EndVBoxContainer();

        UI.EndVBoxContainer();
    }
}