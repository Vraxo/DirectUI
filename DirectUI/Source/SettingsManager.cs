using System;
using System.IO;
using System.Text.Json;

namespace DirectUI;

/// <summary>
/// Manages loading and saving of global DirectUI settings.
/// </summary>
internal static class SettingsManager
{
    private static readonly string SettingsDirectory = Path.Combine(AppContext.BaseDirectory, "Data");
    private static readonly string SettingsFilePath = Path.Combine(SettingsDirectory, "settings.json");

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Saves the provided settings object to the default settings file.
    /// </summary>
    public static void SaveSettings(DirectUISettings settings)
    {
        try
        {
            Directory.CreateDirectory(SettingsDirectory);
            string json = JsonSerializer.Serialize(settings, SerializerOptions);
            File.WriteAllText(SettingsFilePath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DirectUI] Error saving settings to {SettingsFilePath}: {ex.Message}");
        }
    }

    /// <summary>
    /// Loads the settings object from the default settings file.
    /// If the file doesn't exist or fails to load, returns a new instance with default values.
    /// </summary>
    public static DirectUISettings LoadSettings()
    {
        if (!File.Exists(SettingsFilePath))
        {
            return new DirectUISettings(); // Return default settings
        }

        try
        {
            string json = File.ReadAllText(SettingsFilePath);
            return JsonSerializer.Deserialize<DirectUISettings>(json, SerializerOptions) ?? new DirectUISettings();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DirectUI] Error loading settings from {SettingsFilePath}. Using defaults. Error: {ex.Message}");
            return new DirectUISettings(); // Return default settings on error
        }
    }
}