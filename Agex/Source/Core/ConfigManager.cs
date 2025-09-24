using System.Text.Json;

namespace Agex.Core;

public static class ConfigManager
{
    private static readonly string UserDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    private static readonly string ConfigDir = Path.Combine(UserDataPath, "Agex", ".config");
    private static readonly string ProjectsFilePath = Path.Combine(ConfigDir, "recent-projects.json");
    private static readonly string SettingsFilePath = Path.Combine(ConfigDir, "app-settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static void Initialize()
    {
        Directory.CreateDirectory(ConfigDir);

        if (!File.Exists(ProjectsFilePath))
        {
            SetRecentProjects(new List<Project>());
        }

        if (!File.Exists(SettingsFilePath))
        {
            SetAppSettings(new AppSettings());
        }
    }

    public static List<Project> GetRecentProjects()
    {
        try
        {
            var json = File.ReadAllText(ProjectsFilePath);
            return JsonSerializer.Deserialize<List<Project>>(json) ?? new List<Project>();
        }
        catch { return new List<Project>(); }
    }

    public static void SetRecentProjects(List<Project> projects)
    {
        var json = JsonSerializer.Serialize(projects, JsonOptions);
        File.WriteAllText(ProjectsFilePath, json);
    }

    public static AppSettings GetAppSettings()
    {
        try
        {
            var json = File.ReadAllText(SettingsFilePath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch { return new AppSettings(); }
    }

    public static void SetAppSettings(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(SettingsFilePath, json);
    }
}

public class AppSettings
{
    public bool AutomaticModeEnabled { get; set; } = false;
    // For now, we only have one provider, but this matches the TS app structure.
    public string SelectedAIProvider { get; set; } = "ai-studio";
}