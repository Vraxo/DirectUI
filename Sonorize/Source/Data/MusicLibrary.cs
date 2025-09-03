// Sonorize/Source/Data/MusicLibrary.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SkiaSharp;
using TagLib;

namespace Sonorize;

public class MusicLibrary
{
    private List<MusicFile> _files = new();
    private List<Playlist> _playlists = new();
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

    public IReadOnlyList<Playlist> Playlists
    {
        get
        {
            lock (_lock)
            {
                return new List<Playlist>(_playlists);
            }
        }
    }


    private static readonly string[] SupportedExtensions = { ".mp3", ".flac", ".m4a", ".ogg", ".wav", ".wma", ".aac" };
    private static readonly string[] PlaylistExtensions = { ".m3u", ".m3u8" };
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

                            // --- Album Art Extraction ---
                            var picture = tagFile.Tag.Pictures.FirstOrDefault();
                            byte[]? albumArtData = picture?.Data.Data;
                            byte[]? abstractArtData = CreateAbstractArt(albumArtData);
                            // --- End Album Art Extraction ---

                            var musicFile = new MusicFile
                            {
                                Title = string.IsNullOrEmpty(tagFile.Tag.Title) ? Path.GetFileNameWithoutExtension(filePath) : tagFile.Tag.Title,
                                Artist = tagFile.Tag.FirstPerformer ?? "Unknown Artist",
                                Album = tagFile.Tag.Album ?? "Unknown Album",
                                Genre = tagFile.Tag.FirstGenre ?? string.Empty,
                                Year = tagFile.Tag.Year,
                                Duration = tagFile.Properties.Duration,
                                FilePath = filePath,
                                AlbumArt = albumArtData,
                                AbstractAlbumArt = abstractArtData
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

            // Now scan for playlists
            var foundPlaylists = new List<Playlist>();
            var musicFileLookup = sortedFiles.ToDictionary(f => Path.GetFullPath(f.FilePath), f => f);

            foreach (var dir in dirsToScan)
            {
                if (!Directory.Exists(dir)) continue;
                try
                {
                    var playlistFilesInDir = Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories)
                        .Where(f => PlaylistExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()));

                    foreach (var playlistPath in playlistFilesInDir)
                    {
                        var playlist = new Playlist
                        {
                            Name = Path.GetFileNameWithoutExtension(playlistPath),
                            FilePath = playlistPath
                        };

                        var playlistDirectory = Path.GetDirectoryName(playlistPath);
                        if (playlistDirectory == null) continue;

                        var lines = System.IO.File.ReadAllLines(playlistPath);
                        foreach (var line in lines)
                        {
                            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#')) continue;

                            string trackPath = line.Trim();
                            if (!Path.IsPathRooted(trackPath))
                            {
                                trackPath = Path.GetFullPath(Path.Combine(playlistDirectory, trackPath));
                            }

                            if (musicFileLookup.TryGetValue(trackPath, out var musicFile))
                            {
                                playlist.Tracks.Add(musicFile);
                            }
                        }

                        if (playlist.Tracks.Any())
                        {
                            foundPlaylists.Add(playlist);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error scanning for playlists in {dir}: {ex.Message}");
                }
            }


            lock (_lock)
            {
                _files = sortedFiles;
                _playlists = foundPlaylists.OrderBy(p => p.Name).ToList();
            }
            Console.WriteLine($"Scan complete. Found {_files.Count} files and {_playlists.Count} playlists.");
        });
    }

    /// <summary>
    /// Creates a 1-pixel-high, blurred strip of the album art's colors.
    /// When stretched vertically, this produces a soft, abstract gradient background.
    /// </summary>
    private static byte[]? CreateAbstractArt(byte[]? originalArtData, int width = 128, float blurSigma = 10f)
    {
        if (originalArtData == null || originalArtData.Length == 0) return null;

        try
        {
            using var originalBitmap = SKBitmap.Decode(originalArtData);
            if (originalBitmap == null) return null;

            // 1. Create a bitmap that is `width` pixels wide and 1 pixel tall.
            var info = new SKImageInfo(width, 1);
            using var resizedBitmap = new SKBitmap(info);

            // 2. Draw the original bitmap into this tiny strip to average the colors.
            using (var canvas = new SKCanvas(resizedBitmap))
            {
                var paint = new SKPaint { FilterQuality = SKFilterQuality.High };
                canvas.DrawBitmap(originalBitmap, new SKRect(0, 0, width, 1), paint);
            }

            // 3. Create a final bitmap and apply a strong horizontal blur to the color strip.
            using var finalBitmap = new SKBitmap(info);
            using (var canvas = new SKCanvas(finalBitmap))
            using (var paint = new SKPaint())
            {
                // We only blur horizontally (sigmaX) to blend the colors in the strip.
                paint.ImageFilter = SKImageFilter.CreateBlur(blurSigma, 0);
                canvas.DrawBitmap(resizedBitmap, 0, 0, paint);
            }

            // 4. Encode the final tiny, blurry strip.
            using var image = SKImage.FromBitmap(finalBitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            return data.ToArray();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Could not create abstract art: {ex.Message}");
            return null;
        }
    }
}