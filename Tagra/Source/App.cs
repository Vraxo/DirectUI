using System.Collections.Generic;
using System.IO;
using System.Linq;
using DirectUI;
using DirectUI.Core;
using Tagra.Data;

namespace Tagra;

public class App : IAppLogic
{
    private readonly IWindowHost _host;
    private readonly DatabaseManager _dbManager;

    // UI State
    private string _searchText = string.Empty;
    private string _lastSearchText = string.Empty;
    private List<Tag> _allTags = new();
    private List<FileEntry> _displayedFiles = new();
    private FileEntry? _selectedFile;
    private float _leftPanelWidth = 200f;
    private float _rightPanelWidth = 250f;
    private string _newTagName = "";

    public App(IWindowHost host)
    {
        _host = host;
        _dbManager = new DatabaseManager();
        LoadInitialData();
    }

    private void LoadInitialData()
    {
        RefreshAllData();
    }

    private void RefreshDataForSelectedFile()
    {
        if (_selectedFile != null)
        {
            _selectedFile.Tags = _dbManager.GetTagsForFile(_selectedFile.Id);
        }
    }

    private void RefreshAllData()
    {
        _allTags = _dbManager.GetAllTags();
        _displayedFiles = string.IsNullOrWhiteSpace(_searchText)
            ? _dbManager.GetAllFiles()
            : _dbManager.GetFilesByTags(_searchText);

        if (_selectedFile != null)
        {
            _selectedFile = _displayedFiles.FirstOrDefault(f => f.Id == _selectedFile.Id);
            RefreshDataForSelectedFile();
        }
    }

    public void DrawUI(UIContext context)
    {
        HandleSearch();
        DrawLeftPanel();
        DrawMainContentPanel();
        DrawRightPanel();
    }

    private void HandleSearch()
    {
        if (_searchText != _lastSearchText)
        {
            _lastSearchText = _searchText;
            _selectedFile = null; // Clear selection on new search
            _displayedFiles = string.IsNullOrWhiteSpace(_searchText)
                ? _dbManager.GetAllFiles()
                : _dbManager.GetFilesByTags(_searchText);
        }
    }

    private void DrawLeftPanel()
    {
        var panelStyle = new BoxStyle { FillColor = new(40, 40, 40, 255), BorderLength = 0f };
        UI.BeginResizableVPanel("main_layout", ref _leftPanelWidth, HAlignment.Left, minWidth: 150, maxWidth: 400, panelStyle: panelStyle);

        var clipRect = UI.Context.Layout.GetCurrentClipRect();
        var innerWidth = clipRect.Width / UI.Context.UIScale;
        var availableHeight = clipRect.Height / UI.Context.UIScale;
        var currentY = UI.Context.Layout.GetCurrentPosition().Y;

        UI.BeginVBoxContainer("left_panel_vbox", UI.Context.Layout.GetCurrentPosition(), gap: 10f);

        UI.Text("search_label", "Search by Tags");
        if (UI.InputText("search_bar", ref _searchText, new Vector2(innerWidth, 28f), placeholderText: "e.g., cat AND vacation").EnterPressed)
        {
            HandleSearch(); // Trigger search explicitly on Enter
        }

        UI.Separator(innerWidth);

        // --- New Tag Creation ---
        UI.BeginHBoxContainer("new_tag_hbox", UI.Context.Layout.GetCurrentPosition(), gap: 5f);
        UI.InputText("new_tag_input", ref _newTagName, new Vector2(innerWidth - 55, 28f), placeholderText: "New Tag Name");
        if (UI.Button("create_tag_btn", "Add", new Vector2(50, 28)))
        {
            if (!string.IsNullOrWhiteSpace(_newTagName))
            {
                _dbManager.AddTag(_newTagName);
                _newTagName = "";
                RefreshAllData();
            }
        }
        UI.EndHBoxContainer();
        // --- End New Tag Creation ---

        UI.Separator(innerWidth);
        UI.Text("tags_label", "All Tags");

        var scrollHeight = availableHeight - (UI.Context.Layout.GetCurrentPosition().Y - currentY);
        UI.BeginScrollableRegion("tags_scroll", new Vector2(innerWidth, scrollHeight), out var scrollInnerWidth);
        foreach (var tag in _allTags)
        {
            if (UI.Button($"tag_{tag.Id}", $"{tag.Name} ({tag.FileCount})", new Vector2(scrollInnerWidth, 24)))
            {
                _searchText = tag.Name; // Click a tag to search for it
            }
        }
        UI.EndScrollableRegion();

        UI.EndVBoxContainer();
        UI.EndResizableVPanel();
    }

    private void DrawMainContentPanel()
    {
        var scale = UI.Context.UIScale;
        var windowWidth = UI.Context.Renderer.RenderTargetSize.X;
        var windowHeight = UI.Context.Renderer.RenderTargetSize.Y;

        var mainPanelX = _leftPanelWidth + 1;
        var mainPanelWidth = windowWidth / scale - mainPanelX - _rightPanelWidth;
        var mainPanelPos = new Vector2(mainPanelX, 0);

        UI.BeginVBoxContainer("main_content", mainPanelPos, gap: 10, minSize: new Vector2(mainPanelWidth, windowHeight / scale));

        if (UI.Button("add_file_btn", "Add File...", new Vector2(120, 28)))
        {
            string? path = FileDialogs.OpenFile();
            if (!string.IsNullOrWhiteSpace(path))
            {
                _dbManager.AddFile(path);
                RefreshAllData(); // Refresh the file list to show the new file
            }
        }

        UI.Separator(mainPanelWidth);

        var scrollHeight = windowHeight / scale - (UI.Context.Layout.GetCurrentPosition().Y - mainPanelPos.Y);
        UI.BeginScrollableRegion("files_scroll", new Vector2(mainPanelWidth, scrollHeight), out var innerWidth);
        foreach (var file in _displayedFiles)
        {
            bool isSelected = _selectedFile?.Id == file.Id;
            if (UI.Button($"file_{file.Id}", Path.GetFileName(file.Path), new Vector2(innerWidth, 24), isActive: isSelected))
            {
                _selectedFile = file;
                RefreshDataForSelectedFile();
            }
        }
        UI.EndScrollableRegion();

        UI.EndVBoxContainer(advanceParentLayout: false);
    }

    private void DrawRightPanel()
    {
        var panelStyle = new BoxStyle { FillColor = new(40, 40, 40, 255), BorderLength = 0f };
        UI.BeginResizableVPanel("details_panel", ref _rightPanelWidth, HAlignment.Right, minWidth: 200, maxWidth: 500, panelStyle: panelStyle);

        var clipRect = UI.Context.Layout.GetCurrentClipRect();
        var innerWidth = clipRect.Width / UI.Context.UIScale;

        if (_selectedFile is null)
        {
            UI.Text("no_selection_label", "Select a file to see details.", new Vector2(innerWidth, 40));
        }
        else
        {
            var availableHeight = clipRect.Height / UI.Context.UIScale;
            UI.BeginScrollableRegion("details_scroll", new Vector2(innerWidth, availableHeight), out var scrollInnerWidth);
            UI.BeginVBoxContainer("details_vbox", Vector2.Zero, gap: 5f);

            UI.WrappedText("selected_path", _selectedFile.Path, new Vector2(scrollInnerWidth, 0));
            UI.Separator(scrollInnerWidth);
            UI.Text("assigned_tags_label", "Assigned Tags:");

            foreach (var tag in _selectedFile.Tags)
            {
                UI.BeginHBoxContainer($"assigned_tag_{tag.Id}", UI.Context.Layout.GetCurrentPosition(), gap: 5);
                if (UI.Button($"remove_tag_{tag.Id}", "x", new Vector2(20, 20)))
                {
                    _dbManager.RemoveTagFromFile(_selectedFile.Id, tag.Id);
                    RefreshDataForSelectedFile();
                }
                UI.Text($"assigned_tag_name_{tag.Id}", tag.Name, new Vector2(scrollInnerWidth - 30, 20));
                UI.EndHBoxContainer();
            }

            UI.Separator(scrollInnerWidth);
            UI.Text("available_tags_label", "Available Tags to Add:");
            var availableTags = _allTags.Except(_selectedFile.Tags, new TagComparer());
            foreach (var tag in availableTags)
            {
                if (UI.Button($"add_tag_{tag.Id}", tag.Name, new Vector2(scrollInnerWidth, 24)))
                {
                    _dbManager.AddTagToFile(_selectedFile.Id, tag.Id);
                    RefreshDataForSelectedFile();
                }
            }

            UI.EndVBoxContainer();
            UI.EndScrollableRegion();
        }

        UI.EndResizableVPanel();
    }

    public void SaveState()
    {
        // This would be used to save app settings, like window size or search history.
    }
}