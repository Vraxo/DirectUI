// DirectUI/Drawing/FontManager.cs
using Raylib_cs;
using System;
using System.Collections.Generic;

namespace DirectUI.Drawing
{
    /// <summary>
    /// Manages loading and caching of fonts for the Raylib backend.
    /// Fonts are registered by name and loaded on-demand at specific sizes.
    /// </summary>
    public static class FontManager
    {
        // Caches the actual loaded Raylib Font objects. Key is (font name, font size).
        private static readonly Dictionary<(string, int), Font> s_loadedFontsCache = new();
        // Stores the registered mapping from a font name to its file path.
        private static readonly Dictionary<string, string> s_fontPaths = new();
        private static bool s_isInitialized = false;

        /// <summary>
        /// Initializes the FontManager.
        /// </summary>
        public static void Initialize()
        {
            if (s_isInitialized) return;
            s_isInitialized = true;
        }

        /// <summary>
        /// Registers a font's name and file path so it can be loaded later.
        /// </summary>
        /// <param name="fontName">The logical name for the font (e.g., "Segoe UI").</param>
        /// <param name="filePath">The path to the font file.</param>
        public static void RegisterFont(string fontName, string filePath)
        {
            if (!s_isInitialized)
            {
                Console.WriteLine("FontManager not initialized. Call FontManager.Initialize() first.");
                return;
            }
            s_fontPaths[fontName] = filePath;
        }

        /// <summary>
        /// Retrieves a font at a specific size. If not cached, it's loaded from its registered path.
        /// </summary>
        /// <param name="fontName">The name of the registered font.</param>
        /// <param name="fontSize">The desired size of the font.</param>
        /// <returns>The Raylib Font object. Returns the default font if the requested font cannot be loaded.</returns>
        public static Font GetFont(string fontName, int fontSize)
        {
            if (!s_isInitialized)
            {
                return Raylib.GetFontDefault();
            }
            if (fontSize <= 0)
            {
                fontSize = 10; // Use a default small size for safety.
            }

            var cacheKey = (fontName, fontSize);
            if (s_loadedFontsCache.TryGetValue(cacheKey, out var font))
            {
                return font;
            }

            if (!s_fontPaths.TryGetValue(fontName, out var filePath))
            {
                Console.WriteLine($"Font '{fontName}' is not registered. Returning default font.");
                return Raylib.GetFontDefault();
            }

            try
            {
                // Load the font at the exact size requested for the atlas.
                Font newFont = Raylib.LoadFontEx(filePath, fontSize, null, 0);

                // CRITICAL FIX: Do NOT generate mipmaps. Mipmaps can cause additional
                // blurring when the GPU blends between different mip levels. For crisp UI text
                // via oversampling, we want to scale down from a single high-resolution texture.
                // Raylib.GenTextureMipmaps(ref newFont.Texture);

                // Use Bilinear filtering for smooth downscaling from the oversized atlas.
                Raylib.SetTextureFilter(newFont.Texture, TextureFilter.Bilinear);

                s_loadedFontsCache[cacheKey] = newFont;
                return newFont;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load font '{fontName}' at size {fontSize} from '{filePath}'. Reason: {ex.Message}. Returning default font.");
                return Raylib.GetFontDefault();
            }
        }

        /// <summary>
        /// Unloads all loaded fonts and clears all caches.
        /// </summary>
        public static void UnloadAll()
        {
            var defaultFont = Raylib.GetFontDefault();
            foreach (var font in s_loadedFontsCache.Values)
            {
                // Do not unload the default font, Raylib manages it.
                if (font.Texture.Id != defaultFont.Texture.Id)
                {
                    Raylib.UnloadFont(font);
                }
            }
            s_loadedFontsCache.Clear();
            s_fontPaths.Clear();
            s_isInitialized = false;
        }
    }
}