using System.Collections.Generic;
using System.IO;
using SkiaSharp;

namespace Tagra;

public class ThumbnailService : IDisposable
{
    private readonly Dictionary<string, byte[]?> _thumbnailCache = new();
    private readonly HashSet<string> _supportedExtensions = new(System.StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".gif"
    };

    public byte[]? GetThumbnail(string filePath, int size = 128)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            return null;
        }

        if (_thumbnailCache.TryGetValue(filePath, out var cachedData))
        {
            return cachedData;
        }

        var extension = Path.GetExtension(filePath);
        if (!_supportedExtensions.Contains(extension))
        {
            _thumbnailCache[filePath] = null;
            return null;
        }

        try
        {
            using var inputStream = new SKFileStream(filePath);
            using var original = SKBitmap.Decode(inputStream);
            if (original is null)
            {
                _thumbnailCache[filePath] = null;
                return null;
            }

            var info = new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Premul);
            using var resized = original.Resize(info, SKFilterQuality.Medium);
            using var image = SKImage.FromBitmap(resized);
            // Encode to PNG as it supports transparency and is widely compatible.
            using var data = image.Encode(SKEncodedImageFormat.Png, 90);

            var bytes = data.ToArray();
            _thumbnailCache[filePath] = bytes;
            return bytes;

        }
        catch
        {
            _thumbnailCache[filePath] = null; // Cache failure so we don't retry
            return null;
        }
    }

    public void Dispose()
    {
        _thumbnailCache.Clear();
    }
}