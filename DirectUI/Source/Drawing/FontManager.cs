// DirectUI/Drawing/FontManager.cs
using Raylib_cs;
using System;
using System.Collections.Generic;
using Vortice.DirectWrite;

namespace DirectUI.Drawing
{
    /// <summary>
    /// Manages loading and caching of fonts for the Raylib backend.
    /// Fonts are registered by family and loaded on-demand at specific sizes and weights.
    /// </summary>
    public static class FontManager
    {
        // Caches the actual loaded Raylib Font objects. Key is (family name, font size, font weight).
        private static readonly Dictionary<(string, int, FontWeight), Font> s_loadedFontsCache = new();

        // Stores registered font families. Key is family name (e.g., "Segoe UI").
        // Value is a dictionary mapping a FontWeight to its file path.
        private static readonly Dictionary<string, Dictionary<FontWeight, string>> s_fontFamilies = new();
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
        /// Registers a specific font variant (a file path for a given weight) to a font family.
        /// </summary>
        /// <param name="familyName">The logical name for the font family (e.g., "Segoe UI").</param>
        /// <param name="weight">The weight of this font variant.</param>
        /// <param name="filePath">The path to the font file for this variant.</param>
        public static void RegisterFontVariant(string familyName, FontWeight weight, string filePath)
        {
            if (!s_isInitialized)
            {
                Console.WriteLine("FontManager not initialized. Call FontManager.Initialize() first.");
                return;
            }
            if (!s_fontFamilies.TryGetValue(familyName, out var variants))
            {
                variants = new Dictionary<FontWeight, string>();
                s_fontFamilies[familyName] = variants;
            }
            variants[weight] = filePath;
        }

        /// <summary>
        /// Retrieves a font at a specific size and weight. If not cached, it's loaded from its registered path.
        /// </summary>
        /// <param name="familyName">The name of the registered font family.</param>
        /// <param name="fontSize">The desired size of the font.</param>
        /// <param name="weight">The desired weight of the font.</param>
        /// <returns>The Raylib Font object. Returns the default font if the requested font cannot be loaded.</returns>
        public static Font GetFont(string familyName, int fontSize, FontWeight weight)
        {
            if (!s_isInitialized)
            {
                return Raylib.GetFontDefault();
            }
            if (fontSize <= 0)
            {
                fontSize = 10; // Use a default small size for safety.
            }

            var cacheKey = (familyName, fontSize, weight);
            if (s_loadedFontsCache.TryGetValue(cacheKey, out var font))
            {
                return font;
            }

            if (!s_fontFamilies.TryGetValue(familyName, out var variants))
            {
                Console.WriteLine($"Font family '{familyName}' is not registered. Returning default font.");
                return Raylib.GetFontDefault();
            }

            // Find the correct file path. Fallback to Normal weight if the requested weight is not available.
            if (!variants.TryGetValue(weight, out var filePath) && !variants.TryGetValue(FontWeight.Normal, out filePath))
            {
                Console.WriteLine($"Font family '{familyName}' does not have a variant for weight {weight} or a Normal fallback. Returning default font.");
                return Raylib.GetFontDefault();
            }

            try
            {
                // Load the font at the exact size requested for the atlas.
                Font newFont = Raylib.LoadFontEx(filePath, fontSize, null, 0);

                // Use Point filtering for sharp, 1:1 pixel rendering from the atlas.
                // This avoids the blurriness/shagginess of Bilinear filtering when not oversampling.
                // MSAA should handle smoothing the final glyph edges.
                Raylib.SetTextureFilter(newFont.Texture, TextureFilter.Point);

                s_loadedFontsCache[cacheKey] = newFont;
                return newFont;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load font '{familyName}' (weight {weight}) at size {fontSize} from '{filePath}'. Reason: {ex.Message}. Returning default font.");
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
            s_fontFamilies.Clear();
            s_isInitialized = false;
        }
    }
}