using System;
using System.Collections.Generic;
using System.Numerics;
using Veldrid;
using PixelFormat = Veldrid.PixelFormat;

// Add new using statements for SixLabors libraries
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.IO;

namespace DirectUI.Backends.Vulkan;

/// <summary>
/// Creates and manages a texture atlas for a single font using SixLabors.ImageSharp.
/// It rasterizes characters to a bitmap and uploads it to a Veldrid texture.
/// </summary>
public class FontAtlas : IDisposable
{
    public Texture AtlasTexture { get; }
    public IReadOnlyDictionary<char, GlyphInfo> Glyphs { get; }
    public float FontSize { get; }

    private readonly Veldrid.GraphicsDevice _gd;

    private const int AtlasSize = 1024;
    private const int Padding = 2;
    private const char FirstChar = ' '; // 32
    private const char LastChar = '~';  // 126

    public struct GlyphInfo
    {
        public RectangleF SourceRect; // UV coordinates
        public Vector2 Size;
        public float Advance;
        public Vector2 Bearing;
    }

    public FontAtlas(Veldrid.GraphicsDevice gd, string fontName, float fontSize)
    {
        _gd = gd;
        FontSize = fontSize;

        var glyphs = new Dictionary<char, GlyphInfo>();
        Glyphs = glyphs;

        // Find and load the font using SixLabors.Fonts
        var fontCollection = new FontCollection();
        FontFamily fontFamily;
        try
        {
            // First, try to find the font installed on the system.
            fontFamily = fontCollection.Get(fontName);
        }
        catch (FontFamilyNotFoundException)
        {
            try
            {
                // If not found, fall back to trying to load from a common Windows fonts path.
                // This makes it more portable than relying on system-installed fonts alone.
                string windowsFontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts", fontName + ".ttf");
                if (File.Exists(windowsFontPath))
                {
                    fontFamily = fontCollection.Add(windowsFontPath);
                }
                else
                {
                    // Fallback to a known system font as a last resort.
                    Console.WriteLine($"Warning: Font '{fontName}' not found. Falling back to Arial.");
                    fontFamily = SystemFonts.Get("Arial");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Critical: Could not load fallback font. {ex.Message}");
                // If even the fallback fails, we must re-throw or handle it gracefully.
                throw new FileNotFoundException("Could not find or load any suitable font.", ex);
            }
        }
        var font = fontFamily.CreateFont(fontSize);

        using var image = new Image<Rgba32>(AtlasSize, AtlasSize);

        int currentX = Padding;
        int currentY = Padding;
        float maxRowHeight = 0;

        var textOptions = new RichTextOptions(font)
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };

        var drawingOptions = new DrawingOptions();

        image.Mutate(ctx =>
        {
            ctx.Fill(SixLabors.ImageSharp.Color.Transparent);

            for (char c = FirstChar; c <= LastChar; c++)
            {
                var charStr = c.ToString();
                var size = TextMeasurer.MeasureSize(charStr, textOptions);

                if (currentX + (int)Math.Ceiling(size.Width) + Padding > AtlasSize)
                {
                    currentY += (int)Math.Ceiling(maxRowHeight) + Padding;
                    currentX = Padding;
                    maxRowHeight = 0;
                }

                // Draw the character onto the atlas
                ctx.DrawText(drawingOptions, charStr, font, SixLabors.ImageSharp.Color.White, new PointF(currentX, currentY));

                var glyph = new GlyphInfo
                {
                    SourceRect = new RectangleF(
                        (float)currentX / AtlasSize,
                        (float)currentY / AtlasSize,
                        size.Width / AtlasSize,
                        size.Height / AtlasSize
                    ),
                    Size = new Vector2(size.Width, size.Height),
                    Advance = size.Width, // This is a simplification. Real advance is more complex.
                    Bearing = Vector2.Zero // Simplified. Real bearing is the offset from the cursor pos to the glyph's top-left.
                };
                glyphs[c] = glyph;

                currentX += (int)Math.Ceiling(size.Width) + Padding;
                maxRowHeight = Math.Max(maxRowHeight, size.Height);
            }
        });

        AtlasTexture = CreateTextureFromImage(image);
    }

    public Vector2 MeasureText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return Vector2.Zero;

        float width = 0;
        float height = 0;
        foreach (char c in text)
        {
            if (Glyphs.TryGetValue(c, out var glyph))
            {
                width += glyph.Advance;
                height = Math.Max(height, glyph.Size.Y);
            }
            else // Handle character not in atlas by using a fallback '?' character
            {
                if (Glyphs.TryGetValue('?', out var fallbackGlyph))
                {
                    width += fallbackGlyph.Advance;
                    height = Math.Max(height, fallbackGlyph.Size.Y);
                }
            }
        }
        return new Vector2(width, height);
    }

    private Texture CreateTextureFromImage(Image<Rgba32> image)
    {
        var texture = _gd.ResourceFactory.CreateTexture(TextureDescription.Texture2D(
            (uint)image.Width, (uint)image.Height, 1, 1,
            PixelFormat.R8_UNorm, TextureUsage.Sampled));

        // We need an R8 texture (single channel for alpha). We extract the alpha channel from our Rgba32 image.
        byte[] alphaBytes = new byte[image.Width * image.Height];

        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                var pixelRow = accessor.GetRowSpan(y);
                for (int x = 0; x < accessor.Width; x++)
                {
                    // Copy the alpha byte.
                    alphaBytes[y * accessor.Width + x] = pixelRow[x].A;
                }
            }
        });

        _gd.UpdateTexture(texture, alphaBytes, 0, 0, 0, (uint)image.Width, (uint)image.Height, 1, 0, 0);

        return texture;
    }

    public void Dispose()
    {
        AtlasTexture.Dispose();
    }
}