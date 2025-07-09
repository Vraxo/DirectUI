// DirectUI/Source/Drawing/FontManager.cs
using Raylib_cs;
using System;
using System.Collections.Generic;

namespace DirectUI.Drawing
{
    /// <summary>
    /// Manages loading and caching of fonts for the Raylib backend.
    /// </summary>
    public static class FontManager
    {
        private static readonly Dictionary<string, Font> s_fontCache = new();
        private static bool s_isInitialized = false;

        /// <summary>
        /// Initializes the FontManager. Must be called after Raylib window is initialized.
        /// </summary>
        public static void Initialize()
        {
            if (s_isInitialized) return;
            
            // Load a default font that is guaranteed to exist.
            // This prevents crashes if a requested font is not found.
            s_fontCache["default"] = Raylib.GetFontDefault();
            s_isInitialized = true;
        }

        /// <summary>
        /// Loads a font from the specified file path and associates it with a given name.
        /// </summary>
        /// <param name="fontName">The logical name to assign to the font (e.g., "Default", "UI_Bold").</param>
        /// <param name="filePath">The path to the font file (e.g., "Assets/Fonts/Roboto-Regular.ttf").</param>
        public static void LoadFont(string fontName, string filePath)
        {
            if (!s_isInitialized)
            {
                Console.WriteLine("FontManager not initialized. Call FontManager.Initialize() first.");
                return;
            }

            if (s_fontCache.ContainsKey(fontName))
            {
                Console.WriteLine($"Font '{fontName}' is already loaded.");
                return;
            }

            try
            {
                // Use LoadFontEx for better quality font rendering, specifying a base font size.
                // A base size of 64 is generally good for UI elements, allowing Raylib to scale down cleanly.
                // The '0' for fontChars allows Raylib to load default ASCII characters.
                Font font = Raylib.LoadFontEx(filePath, 64, null, 0);
                s_fontCache[fontName] = font;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load font '{fontName}' from '{filePath}'. Reason: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves a loaded font by its logical name.
        /// </summary>
        /// <param name="fontName">The name of the font to retrieve.</param>
        /// <returns>The Raylib Font object. Returns the default font if the named font is not found.</returns>
        public static Font GetFont(string fontName)
        {
            if (!s_isInitialized)
            {
                // Fallback to default font if not initialized, to prevent crashes.
                return Raylib.GetFontDefault();
            }

            if (s_fontCache.TryGetValue(fontName, out var font))
            {
                return font;
            }

            Console.WriteLine($"Font '{fontName}' not found. Returning default font.");
            return s_fontCache["default"];
        }

        /// <summary>
        /// Unloads all loaded fonts and clears the cache.
        /// </summary>
        public static void UnloadAll()
        {
            foreach (var font in s_fontCache.Values)
            {
                // Do not unload the default font, Raylib manages it.
                if (font.Texture.Id != Raylib.GetFontDefault().Texture.Id)
                {
                    Raylib.UnloadFont(font);
                }
            }
            s_fontCache.Clear();
            s_isInitialized = false;
        }
    }
}
