using System.Numerics;
using SixLabors.Fonts;
using SixLabors.Fonts.Unicode;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Veldrid;
using PixelFormat = Veldrid.PixelFormat;

namespace DirectUI.Backends.Vulkan;


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

        var fontCollection = new FontCollection();
        FontFamily fontFamily;
        try
        {
            fontFamily = fontCollection.Get(fontName);
        }
        catch (FontFamilyNotFoundException)
        {
            try
            {
                string windowsFontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts", fontName + ".ttf");
                string SegoeUIPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts", "segoeui.ttf");
                if (File.Exists(windowsFontPath))
                {
                    fontFamily = fontCollection.Add(windowsFontPath);
                }
                else if (File.Exists(SegoeUIPath))
                {
                    Console.WriteLine($"Warning: Font '{fontName}' not found. Falling back to Segoe UI.");
                    fontFamily = fontCollection.Add(SegoeUIPath);
                }
                else
                {
                    Console.WriteLine($"Warning: Font '{fontName}' and Segoe UI not found. Falling back to system default.");
                    fontFamily = SystemFonts.Get("Arial");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Critical: Could not load fallback font. {ex.Message}");
                throw new FileNotFoundException("Could not find or load any suitable font.", ex);
            }
        }
        var font = fontFamily.CreateFont(fontSize);
        float scale = fontSize / font.FontMetrics.UnitsPerEm;

        using var image = new Image<L8>(AtlasSize, AtlasSize);

        int currentX = Padding;
        int currentY = Padding;
        int maxRowHeight = 0;

        for (char c = FirstChar; c <= LastChar; c++)
        {
            if (font.TryGetGlyphs(new CodePoint(c), out IReadOnlyList<Glyph>? glyphsList) && glyphsList is not null && glyphsList.Count > 0)
            {
                var glyph = glyphsList[0];
                using var glyphImage = glyph.RenderToImage<L8>();

                if (currentX + glyphImage.Width + Padding > AtlasSize)
                {
                    currentY += maxRowHeight + Padding;
                    currentX = Padding;
                    maxRowHeight = 0;
                }

                if (glyphImage.Width > 0 && glyphImage.Height > 0)
                {
                    image.Mutate(ctx => ctx.DrawImage(glyphImage, new SixLabors.ImageSharp.Point(currentX, currentY), 1f));
                }

                var glyphInfo = new GlyphInfo
                {
                    SourceRect = new RectangleF(
                        (float)currentX / AtlasSize,
                        (float)currentY / AtlasSize,
                        (float)glyphImage.Width / AtlasSize,
                        (float)glyphImage.Height / AtlasSize
                    ),
                    Size = new Vector2(glyphImage.Width, glyphImage.Height),
                    Advance = glyph.GlyphMetrics.AdvanceWidth * scale,
                    Bearing = new Vector2(glyph.GlyphMetrics.LeftSideBearing * scale, -glyph.GlyphMetrics.TopSideBearing * scale)
                };
                glyphs[c] = glyphInfo;

                currentX += glyphImage.Width + Padding;
                maxRowHeight = Math.Max(maxRowHeight, glyphImage.Height);
            }
        }

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
                // Use the glyph bitmap height for max height calculation
                height = Math.Max(height, glyph.Size.Y);
            }
            else
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

    private Texture CreateTextureFromImage(Image<L8> image)
    {
        var texture = _gd.ResourceFactory.CreateTexture(TextureDescription.Texture2D(
            (uint)image.Width, (uint)image.Height, 1, 1,
            PixelFormat.R8_UNorm, TextureUsage.Sampled));

        byte[] pixelBytes = new byte[image.Width * image.Height];
        image.CopyPixelDataTo(pixelBytes);

        _gd.UpdateTexture(texture, pixelBytes, 0, 0, 0, (uint)image.Width, (uint)image.Height, 1, 0, 0);

        return texture;
    }

    public void Dispose()
    {
        AtlasTexture.Dispose();
    }
}