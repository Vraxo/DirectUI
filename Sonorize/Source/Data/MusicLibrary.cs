// Sonorize/Source/Data/MusicLibrary.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TagLib;

namespace Sonorize;

public class MusicLibrary
{
    private List<MusicFile> _files = new();
    private readonly object _lock = new();

    public IReadOnlyList<MusicFile> Files
    {
        get
        {
            lock (_lock)
            {
                // Return a copy to prevent collection modified exceptions if the background task updates it during a render.
                return new List<MusicFile>(_files);
            }
        }
    }

    private static readonly string[] SupportedExtensions = { ".mp3", ".flac", ".m4a", ".ogg", ".wav", ".wma", ".aac" };
    private Task? _scanTask;

    public void ScanDirectoriesAsync(IEnumerable<string> directories)
    {
        // Don't start a new scan if one is already running
        if (_scanTask is not null && !_scanTask.IsCompleted) return;

        var dirsToScan = directories.ToList(); // Create a copy

        _scanTask = Task.Run(() =>
        {
            Console.WriteLine("Starting library scan...");
            var foundFiles = new List<MusicFile>();
            foreach (var dir in dirsToScan)
            {
                if (!Directory.Exists(dir)) continue;

                try
                {
                    var filesInDir = Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories)
                        .Where(f => SupportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()));

                    foreach (var filePath in filesInDir)
                    {
                        try
                        {
                            using var tagFile = TagLib.File.Create(filePath);
                            var musicFile = new MusicFile
                            {
                                Title = string.IsNullOrEmpty(tagFile.Tag.Title) ? Path.GetFileNameWithoutExtension(filePath) : tagFile.Tag.Title,
                                Artist = tagFile.Tag.FirstPerformer ?? "Unknown Artist",
                                Album = tagFile.Tag.Album ?? "Unknown Album",
                                Genre = tagFile.Tag.FirstGenre ?? string.Empty,
                                Year = tagFile.Tag.Year,
                                Duration = tagFile.Properties.Duration,
                                FilePath = filePath
                            };
                            foundFiles.Add(musicFile);
                        }
                        catch (CorruptFileException) { /* ignore */ }
                        catch (UnsupportedFormatException) { /* ignore */ }
                        catch (Exception ex) { Console.WriteLine($"Could not read tag for {filePath}: {ex.Message}"); }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error scanning directory {dir}: {ex.Message}");
                }
            }

            var sortedFiles = foundFiles
                .OrderBy(f => f.Artist)
                .ThenBy(f => f.Album)
                .ThenBy(f => f.Title)
                .ToList();

            lock (_lock)
            {
                _files = sortedFiles;
            }
            Console.WriteLine($"Scan complete. Found {_files.Count} files.");
        });
    }
}