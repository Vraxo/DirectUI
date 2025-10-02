using System.Collections.Generic;
using System.Linq;
using DirectUI;
using DirectUI.Core;
using Tagra.Data;

namespace Tagra;

public class App : IAppLogic
{
    private readonly UIManager _uiManager;

    // --- Services ---
    internal readonly IWindowHost Host;
    internal readonly DatabaseManager DbManager;
    internal readonly ThumbnailService ThumbnailService;

    // --- UI State (internal so UIManager can access it) ---
    internal string SearchText = string.Empty;
    internal string LastSearchText = string.Empty;
    internal List<Tag> AllTags = new();
    internal List<FileEntry> DisplayedFiles = new();
    internal FileEntry? SelectedFile;
    internal float LeftPanelWidth = 200f;
    internal float RightPanelWidth = 250f;
    internal string NewTagName = "";
    internal long? TagIdToDelete = null;

    public App(IWindowHost host)
    {
        Host = host;
        DbManager = new DatabaseManager();
        ThumbnailService = new ThumbnailService();
        _uiManager = new UIManager(this);
        LoadInitialData();
    }

    private void LoadInitialData()
    {
        RefreshAllData();
    }

    internal void RefreshDataForSelectedFile()
    {
        if (SelectedFile != null)
        {
            SelectedFile.Tags = DbManager.GetTagsForFile(SelectedFile.Id);
        }
    }

    internal void RefreshAllData()
    {
        AllTags = DbManager.GetAllTags();
        DisplayedFiles = string.IsNullOrWhiteSpace(SearchText)
            ? DbManager.GetAllFiles()
            : DbManager.GetFilesByTags(SearchText);

        if (SelectedFile != null)
        {
            SelectedFile = DisplayedFiles.FirstOrDefault(f => f.Id == SelectedFile.Id);
            RefreshDataForSelectedFile();
        }
    }

    public void DrawUI(UIContext context)
    {
        // Handle modal window logic at the top level
        if (TagIdToDelete.HasValue && !Host.ModalWindowService.IsModalWindowOpen)
        {
            var tagToDelete = AllTags.FirstOrDefault(t => t.Id == TagIdToDelete.Value);
            if (tagToDelete != null)
            {
                Host.ModalWindowService.OpenModalWindow(
                    "Confirm Deletion",
                    300, 120,
                    (modalContext) => _uiManager.DrawDeleteConfirmationModal(tagToDelete),
                    (result) => {
                        if (result == 0) // 'Yes' was clicked
                        {
                            DbManager.DeleteTag(TagIdToDelete.Value);
                            RefreshAllData();
                        }
                        TagIdToDelete = null;
                    }
                );
            }
            else
            {
                TagIdToDelete = null; // Tag not found
            }
        }

        HandleSearch();
        _uiManager.DrawLayout();
    }

    internal void HandleSearch()
    {
        if (SearchText != LastSearchText)
        {
            LastSearchText = SearchText;
            SelectedFile = null; // Clear selection on new search
            DisplayedFiles = string.IsNullOrWhiteSpace(SearchText)
                ? DbManager.GetAllFiles()
                : DbManager.GetFilesByTags(SearchText);
        }
    }

    public void SaveState()
    {
        // This would be used to save app settings, like window size or search history.
    }
}