using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using Microsoft.Xna.Framework.Graphics;
using XnaColor = Microsoft.Xna.Framework.Color;
using XnaRectangle = Microsoft.Xna.Framework.Rectangle;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace Strange_Universe.Game.Systems;

/// <summary>
/// Builds a <see cref="SpriteFont"/> at runtime by rasterizing a system font into a
/// texture atlas, so no <c>.spritefont</c> content pipeline asset is required.
/// </summary>
public static class FontBuilder
{
    private const char FirstChar = ' ';
    private const char LastChar  = '~';
    private const int  Padding   = 2;

    /// <summary>
    /// Rasterizes <paramref name="fontFamily"/> at <paramref name="sizeInPoints"/> and
    /// returns a ready-to-use <see cref="SpriteFont"/> covering the printable ASCII range.
    /// </summary>
    public static SpriteFont Build(
        GraphicsDevice gd,
        string fontFamily = "Arial",
        float sizeInPoints = 16f,
        FontStyle style = FontStyle.Regular,
        float spacing = 0f)
    {
        using var font = CreateFont(fontFamily, sizeInPoints, style);

        var characters  = new List<char>();
        var glyphSizes  = new List<Size>();

        using (var measureBitmap = new Bitmap(1, 1))
        using (var measureGraphics = Graphics.FromImage(measureBitmap))
        {
            measureGraphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            // GenericTypographic reports a width of 0 for whitespace, so advances are
            // measured with a delimiter pair and the baseline width subtracted.
            float delimiterWidth = measureGraphics.MeasureString(
                "||", font, PointF.Empty, StringFormat.GenericTypographic).Width;

            for (char c = FirstChar; c <= LastChar; c++)
            {
                SizeF size = measureGraphics.MeasureString(
                    c.ToString(), font, PointF.Empty, StringFormat.GenericTypographic);

                float advance = measureGraphics.MeasureString(
                    "|" + c + "|", font, PointF.Empty, StringFormat.GenericTypographic).Width
                    - delimiterWidth;

                int w = Math.Max(1, (int)MathF.Ceiling(Math.Max(size.Width, advance)));
                int h = Math.Max(1, (int)MathF.Ceiling(Math.Max(size.Height, font.Height)));

                characters.Add(c);
                glyphSizes.Add(new Size(w, h));
            }
        }

        int lineSpacing = font.Height;
        int cellHeight  = 0;
        foreach (var s in glyphSizes)
            cellHeight = Math.Max(cellHeight, s.Height);

        // Lay the glyphs out in a roughly square atlas.
        int atlasWidth = 256;
        int totalWidth = 0;
        foreach (var s in glyphSizes)
            totalWidth += s.Width + Padding;
        while (atlasWidth * atlasWidth < totalWidth * (cellHeight + Padding))
            atlasWidth *= 2;

        var glyphBounds = new List<Rectangle>(characters.Count);
        int penX = Padding;
        int penY = Padding;
        int atlasHeight = penY + cellHeight + Padding;

        foreach (var s in glyphSizes)
        {
            if (penX + s.Width + Padding > atlasWidth)
            {
                penX = Padding;
                penY += cellHeight + Padding;
                atlasHeight = penY + cellHeight + Padding;
            }

            glyphBounds.Add(new Rectangle(penX, penY, s.Width, cellHeight));
            penX += s.Width + Padding;
        }

        atlasHeight = NextPowerOfTwo(atlasHeight);

        XnaColor[] pixels;
        using (var bitmap = new Bitmap(atlasWidth, atlasHeight, PixelFormat.Format32bppArgb))
        {
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(System.Drawing.Color.Transparent);
                graphics.TextRenderingHint  = TextRenderingHint.AntiAliasGridFit;
                graphics.SmoothingMode      = SmoothingMode.HighQuality;
                graphics.InterpolationMode  = InterpolationMode.HighQualityBicubic;

                for (int i = 0; i < characters.Count; i++)
                {
                    graphics.DrawString(
                        characters[i].ToString(),
                        font,
                        Brushes.White,
                        new PointF(glyphBounds[i].X, glyphBounds[i].Y),
                        StringFormat.GenericTypographic);
                }
            }

            pixels = ToPremultipliedColors(bitmap);
        }

        var texture = new Texture2D(gd, atlasWidth, atlasHeight, false, SurfaceFormat.Color);
        texture.SetData(pixels);

        var xnaGlyphBounds = new List<XnaRectangle>(characters.Count);
        var cropping       = new List<XnaRectangle>(characters.Count);
        var kerning        = new List<Vector3>(characters.Count);

        for (int i = 0; i < characters.Count; i++)
        {
            Rectangle b = glyphBounds[i];
            xnaGlyphBounds.Add(new XnaRectangle(b.X, b.Y, b.Width, b.Height));
            cropping.Add(new XnaRectangle(0, 0, b.Width, b.Height));
            kerning.Add(new Vector3(0f, b.Width, 0f));
        }

        return new SpriteFont(
            texture,
            xnaGlyphBounds,
            cropping,
            characters,
            lineSpacing,
            spacing,
            kerning,
            '?');
    }

    private static Font CreateFont(string fontFamily, float sizeInPoints, FontStyle style)
    {
        try
        {
            var family = new FontFamily(fontFamily);
            if (!family.IsStyleAvailable(style))
                style = FontStyle.Regular;
            return new Font(family, sizeInPoints, style, GraphicsUnit.Pixel);
        }
        catch (ArgumentException)
        {
            return new Font(FontFamily.GenericSansSerif, sizeInPoints, FontStyle.Regular, GraphicsUnit.Pixel);
        }
    }

    private static XnaColor[] ToPremultipliedColors(Bitmap bitmap)
    {
        var data = bitmap.LockBits(
            new Rectangle(0, 0, bitmap.Width, bitmap.Height),
            ImageLockMode.ReadOnly,
            PixelFormat.Format32bppArgb);

        var pixels = new XnaColor[bitmap.Width * bitmap.Height];
        try
        {
            var buffer = new byte[data.Stride * bitmap.Height];
            System.Runtime.InteropServices.Marshal.Copy(data.Scan0, buffer, 0, buffer.Length);

            for (int y = 0; y < bitmap.Height; y++)
            {
                int row = y * data.Stride;
                for (int x = 0; x < bitmap.Width; x++)
                {
                    byte b = buffer[row + x * 4 + 0];
                    byte g = buffer[row + x * 4 + 1];
                    byte r = buffer[row + x * 4 + 2];
                    byte a = buffer[row + x * 4 + 3];

                    pixels[y * bitmap.Width + x] = XnaColor.FromNonPremultiplied(r, g, b, a);
                }
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        return pixels;
    }

    private static int NextPowerOfTwo(int value)
    {
        int result = 1;
        while (result < value)
            result <<= 1;
        return result;
    }
}
