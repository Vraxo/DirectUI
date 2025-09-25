using System.IO;
using System.Text.Json;

namespace Bankan;

public static class StateSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static void Save<T>(T data, string filePath)
    {
        try
        {
            string json = JsonSerializer.Serialize(data, Options);
            File.WriteAllText(filePath, json);
        }
        catch (System.Exception ex)
        {
            System.Console.WriteLine($"Error saving state to {filePath}: {ex.Message}");
        }
    }

    public static T? Load<T>(string filePath) where T : class, new()
    {
        if (!File.Exists(filePath))
        {
            return new T(); // Return a new instance if file doesn't exist
        }

        try
        {
            string json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<T>(json, Options) ?? new T();
        }
        catch (System.Exception ex)
        {
            System.Console.WriteLine($"Error loading state from {filePath}: {ex.Message}");
            return new T(); // Return a new instance on error
        }
    }
}