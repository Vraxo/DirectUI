using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Vortice.Mathematics;

namespace DirectUI;

public class BottomPanelView
{
    private readonly string _basePath = @"D:\Parsa Stuff\Visual Studio\Cosmocrush\Cosmocrush\Res";
    private readonly List<string> _directories;
    private readonly List<string> _files;
    private string? _errorMessage;

    private readonly ButtonStylePack _folderIconStyle;
    private readonly ButtonStylePack _fileIconStyle;
    private readonly ButtonStyle _labelStyle;

    public BottomPanelView()
    {
        _folderIconStyle = new ButtonStylePack { Roundness = 0.2f, BorderLength = 1f };
        _folderIconStyle.Normal.FillColor = new Color4(0.3f, 0.4f, 0.6f, 1f); // Blueish for folders
        _folderIconStyle.Hover.FillColor = new Color4(0.4f, 0.5f, 0.7f, 1f);
        _folderIconStyle.Pressed.FillColor = new Color4(0.5f, 0.6f, 0.8f, 1f);

        _fileIconStyle = new ButtonStylePack { Roundness = 0.2f, BorderLength = 1f };
        _fileIconStyle.Normal.FillColor = new Color4(0.4f, 0.4f, 0.4f, 1f); // Grey for files
        _fileIconStyle.Hover.FillColor = new Color4(0.5f, 0.5f, 0.5f, 1f);
        _fileIconStyle.Pressed.FillColor = new Color4(0.6f, 0.6f, 0.6f, 1f);

        _labelStyle = new ButtonStyle
        {
            FontColor = DefaultTheme.Text,
            FontSize = 12f
        };

        _directories = new List<string>();
        _files = new List<string>();

        LoadDirectoryContents();
    }

    private void LoadDirectoryContents()
    {
        try
        {
            if (Directory.Exists(_basePath))
            {
                _directories.AddRange(Directory.GetDirectories(_basePath).Select(Path.GetFileName).Where(s => s is not null)!);
                _files.AddRange(Directory.GetFiles(_basePath).Select(Path.GetFileName).Where(s => s is not null)!);
                _directories.Sort(StringComparer.OrdinalIgnoreCase);
                _files.Sort(StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                _errorMessage = $"Path not found: {_basePath}";
            }
        }
        catch (Exception ex)
        {
            _errorMessage = $"Error accessing path '{_basePath}': {ex.Message}";
        }
    }

    public void Draw()
    {
        var contentArea = UI.Context.Layout.GetCurrentClipRect();
        if (contentArea.Width <= 0 || contentArea.Height <= 0)
        {
            return;
        }

        if (!string.IsNullOrEmpty(_errorMessage))
        {
            UI.Label("bottom_panel_error", _errorMessage);
            return;
        }

        var scrollableSize = new Vector2(contentArea.Width, contentArea.Height);
        UI.BeginScrollableRegion("bottom_panel_scroll", scrollableSize, out float innerWidth);
        {
            var gridStartPosition = UI.Context.Layout.GetCurrentPosition();
            var gridAvailableSize = new Vector2(innerWidth, 10000);
            var gridGap = new Vector2(16, 16);
            int numColumns = Math.Max(1, (int)(innerWidth / 90));

            UI.BeginGridContainer("bottom_panel_grid", gridStartPosition, gridAvailableSize, numColumns, gridGap);
            {
                foreach (var dirName in _directories)
                {
                    DrawFileSystemEntry(dirName, true);
                }

                foreach (var fileName in _files)
                {
                    DrawFileSystemEntry(fileName, false);
                }
            }
            UI.EndGridContainer();
        }
        UI.EndScrollableRegion();
    }

    private void DrawFileSystemEntry(string name, bool isDirectory)
    {
        string id = (isDirectory ? "dir_" : "file_") + name;
        var iconSize = new Vector2(64, 64);
        var labelSize = new Vector2(iconSize.X + 10, 30);

        UI.BeginVBoxContainer(id + "_vbox", UI.Context.Layout.GetCurrentPosition(), 4);
        {
            string iconText = isDirectory ? "D" : "F";
            var style = isDirectory ? _folderIconStyle : _fileIconStyle;

            if (UI.Button(id + "_icon", iconText, size: iconSize, theme: style))
            {
                // Future: Handle click (e.g., navigate into directory)
            }

            UI.Label(
                id + "_label",
                name,
                size: labelSize,
                style: _labelStyle,
                textAlignment: new Alignment(HAlignment.Center, VAlignment.Top)
            );
        }
        UI.EndVBoxContainer();
    }
}