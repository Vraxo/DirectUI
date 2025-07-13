using System;
using System.Collections.Generic;
using Veldrid;

namespace DirectUI.Backends.Vulkan;

/// <summary>
/// Manages and caches FontAtlas instances for the Veldrid backend.
/// </summary>
public class VulkanFontManager : IDisposable
{
    private readonly Veldrid.GraphicsDevice _gd;
    private readonly Dictionary<(string, float), FontAtlas> _atlasCache = new();

    public VulkanFontManager(Veldrid.GraphicsDevice gd)
    {
        _gd = gd;
    }

    public FontAtlas GetAtlas(string fontName, float fontSize)
    {
        var key = (fontName, fontSize);
        if (!_atlasCache.TryGetValue(key, out var atlas))
        {
            atlas = new FontAtlas(_gd, fontName, fontSize);
            _atlasCache[key] = atlas;
        }
        return atlas;
    }

    public void Dispose()
    {
        foreach (var atlas in _atlasCache.Values)
        {
            atlas.Dispose();
        }
        _atlasCache.Clear();
    }
}