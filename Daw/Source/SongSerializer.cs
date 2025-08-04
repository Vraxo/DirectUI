using System;
using System.IO;
using System.Text.Json;

namespace Daw.Core;

/// <summary>
/// Handles saving and loading Song objects to and from JSON files.
/// </summary>
public static class SongSerializer
{
    private static readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Saves a Song object to a specified JSON file path.
    /// </summary>
    /// <param name="song">The song to save.</param>
    /// <param name="filePath">The path of the file (e.g., "mysong.dawjson").</param>
    public static void Save(Song song, string filePath)
    {
        try
        {
            string jsonString = JsonSerializer.Serialize(song, _options);
            File.WriteAllText(filePath, jsonString);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving song to '{filePath}': {ex.Message}");
            // Depending on the application, you might want to re-throw or handle this differently.
        }
    }

    /// <summary>
    /// Loads a Song object from a specified JSON file path.
    /// </summary>
    /// <param name="filePath">The path of the file to load.</param>
    /// <returns>The loaded Song object, or null if loading fails.</returns>
    public static Song? Load(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"Error: File not found at '{filePath}'.");
            return null;
        }

        try
        {
            string jsonString = File.ReadAllText(filePath);
            var song = JsonSerializer.Deserialize<Song>(jsonString, _options);
            return song;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading song from '{filePath}': {ex.Message}");
            return null;
        }
    }
}