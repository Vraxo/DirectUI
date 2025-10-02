using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
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
    internal bool ManageTagsRequested = false;
    internal bool SettingsRequested = false;
    internal long? ActiveColorPickerTagId = null; // State for the color picker

    // --- App Settings ---
    internal TagraSettings Settings;

    public App(IWindowHost host)
    {
        Host = host;
        Settings = TagraSettingsManager.Load();
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
        // Draw the menu bar first. It might set ManageTagsRequested = true.
        _uiManager.DrawMenuBar();

        // Handle opening the modal window for tag management
        if (ManageTagsRequested && !Host.ModalWindowService.IsModalWindowOpen)
        {
            Host.ModalWindowService.OpenModalWindow(
                "Manage Tags",
                400, 480,
                (modalContext) => TagManagementWindow.Draw(this),
                (result) => {
                    ManageTagsRequested = false; // Reset the request
                    NewTagName = ""; // Clear any leftover input
                    TagIdToDelete = null; // Clear any leftover state from the modal
                    ActiveColorPickerTagId = null; // Clear color picker state
                    RefreshAllData(); // Refresh data in main window after modal closes
                }
            );
        }

        // Handle opening the settings window
        if (SettingsRequested && !Host.ModalWindowService.IsModalWindowOpen)
        {
            Host.ModalWindowService.OpenModalWindow(
                "Settings",
                300, 200,
                (modalContext) => ModalDialogs.DrawSettingsWindow(this),
                (result) =>
                {
                    SettingsRequested = false;
                    TagraSettingsManager.Save(Settings); // Save settings when window closes
                }
            );
        }

        HandleSearch();
        _uiManager.DrawMainLayout();
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
        // Save application-specific settings
        TagraSettingsManager.Save(Settings);
    }
}


public enum TagDisplayMode { ColorCircle, Emoji }

public class TagraSettings
{
    public TagDisplayMode TagDisplay { get; set; } = TagDisplayMode.ColorCircle;
}

public static class TagraSettingsManager
{
    private static readonly string SettingsDirectory = Path.Combine(AppContext.BaseDirectory, "Data");
    private static readonly string SettingsFilePath = Path.Combine(SettingsDirectory, "tagra_settings.json");

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static void Save(TagraSettings settings)
    {
        try
        {
            Directory.CreateDirectory(SettingsDirectory);
            string json = JsonSerializer.Serialize(settings, SerializerOptions);
            File.WriteAllText(SettingsFilePath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Tagra] Error saving settings: {ex.Message}");
        }
    }

    public static TagraSettings Load()
    {
        if (!File.Exists(SettingsFilePath))
        {
            return new TagraSettings();
        }

        try
        {
            string json = File.ReadAllText(SettingsFilePath);
            return JsonSerializer.Deserialize<TagraSettings>(json, SerializerOptions) ?? new TagraSettings();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Tagra] Error loading settings. Using defaults. Error: {ex.Message}");
            return new TagraSettings();
        }
    }
}