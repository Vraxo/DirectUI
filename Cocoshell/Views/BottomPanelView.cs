using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Vortice.Mathematics;

namespace DirectUI;

public class BottomPanelView
{
    private readonly string _rootPath = @"D:\Parsa Stuff\Visual Studio\Cosmocrush\Cosmocrush\Res";
    private string _currentPath;
    private string? _pathToNavigateTo; // Used to defer navigation until after drawing

    private readonly List<string> _directories;
    private readonly List<string> _files;
    private string? _errorMessage;

    private readonly ButtonStylePack _folderIconStyle;
    private readonly ButtonStylePack _fileIconStyle;
    private readonly ButtonStyle _labelStyle;
    private readonly ButtonStyle _pathLabelStyle;

    public string? SelectedScenePath { get; private set; }

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
            FontSize = 12f // Corrected font size for labels to fit better
        };

        _pathLabelStyle = new ButtonStyle
        {
            FontColor = new Color4(0.7f, 0.7f, 0.7f, 1f),
            FontSize = 12f
        };

        _directories = new List<string>();
        _files = new List<string>();
        _currentPath = _rootPath;

        LoadDirectoryContents();
    }

    private void LoadDirectoryContents()
    {
        _directories.Clear();
        _files.Clear();
        _errorMessage = null;

        try
        {
            if (Directory.Exists(_currentPath))
            {
                // Add an "up" directory if we're not at the root
                if (Path.GetFullPath(_currentPath).TrimEnd('\\') != Path.GetFullPath(_rootPath).TrimEnd('\\'))
                {
                    _directories.Add("..");
                }

                _directories.AddRange(Directory.GetDirectories(_currentPath).Select(Path.GetFileName).Where(s => s is not null)!);
                _files.AddRange(Directory.GetFiles(_currentPath).Select(Path.GetFileName).Where(s => s is not null)!);

                // Keep ".." at the top, sort the rest
                var sortedDirs = _directories.Where(d => d != "..").OrderBy(d => d, StringComparer.OrdinalIgnoreCase).ToList();
                _directories.RemoveAll(d => d != "..");
                _directories.AddRange(sortedDirs);

                _files.Sort(StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                _errorMessage = $"Path not found: {_currentPath}";
            }
        }
        catch (Exception ex)
        {
            _errorMessage = $"Error accessing path '{_currentPath}': {ex.Message}";
        }
    }

    private static string TruncatePathForDisplay(string path, int maxLength)
    {
        if (string.IsNullOrEmpty(path) || path.Length <= maxLength)
        {
            return path;
        }
        // A more robust truncation that handles short max lengths gracefully.
        if (maxLength <= 3)
        {
            return path.Substring(0, maxLength);
        }
        return "..." + path.Substring(path.Length - maxLength + 3);
    }

    public void Draw()
    {
        _pathToNavigateTo = null; // Reset deferred action at the start of the frame
        SelectedScenePath = null; // Reset selected scene path at start of frame

        var contentArea = UI.Context.Layout.GetCurrentClipRect();
        if (contentArea.Width <= 0 || contentArea.Height <= 0)
        {
            return;
        }

        UI.BeginVBoxContainer("bottom_panel_main_vbox", UI.Context.Layout.GetCurrentPosition(), 5);

        // Truncate the path label to a safe length before rendering.
        string displayPath = TruncatePathForDisplay(_currentPath, 60);
        UI.Text("current_path_label", displayPath, style: _pathLabelStyle);

        if (!string.IsNullOrEmpty(_errorMessage))
        {
            // Truncate the error message as well.
            string displayError = TruncatePathForDisplay(_errorMessage, 60);
            UI.Text("bottom_panel_error", displayError);
        }
        else
        {
            var scrollableSize = new Vector2(contentArea.Width, Math.Max(0, contentArea.Height - 30));
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

        // --- Deferred Action ---
        // After all drawing loops are complete, check if navigation was requested.
        // This prevents modifying the collection while it's being enumerated.
        if (_pathToNavigateTo != null)
        {
            _currentPath = _pathToNavigateTo;
            LoadDirectoryContents();
        }

        UI.EndVBoxContainer();
    }

    private void DrawFileSystemEntry(string name, bool isDirectory)
    {
        string id = (isDirectory ? "dir_" : "file_") + name;
        var iconSize = new Vector2(64, 64);
        var labelSize = new Vector2(iconSize.X + 10, 20); // Reduced height for smaller font

        UI.BeginVBoxContainer(id + "_vbox", UI.Context.Layout.GetCurrentPosition(), 4);
        {
            string iconText = isDirectory ? (name == ".." ? ".." : "D") : "F";
            var style = isDirectory ? _folderIconStyle : _fileIconStyle;

            if (UI.Button(id + "_icon", iconText, size: iconSize, theme: style))
            {
                if (isDirectory)
                {
                    if (name == "..")
                    {
                        var parentDir = Directory.GetParent(_currentPath);
                        if (parentDir != null)
                        {
                            _pathToNavigateTo = parentDir.FullName; // Defer navigation
                        }
                    }
                    else
                    {
                        _pathToNavigateTo = Path.Combine(_currentPath, name); // Defer navigation
                    }
                }
                else
                {
                    // If a file is clicked, check its extension.
                    if (Path.GetExtension(name).Equals(".yaml", StringComparison.OrdinalIgnoreCase))
                    {
                        SelectedScenePath = Path.Combine(_currentPath, name);
                    }
                }
            }

            // Truncate the display name before rendering to prevent buffer overflow.
            string displayName = TruncatePathForDisplay(name, 12);

            UI.Text(
                id + "_label",
                displayName, // Use the truncated name
                size: labelSize,
                style: _labelStyle,
                textAlignment: new Alignment(HAlignment.Center, VAlignment.Top)
            );
        }
        UI.EndVBoxContainer();
    }
}