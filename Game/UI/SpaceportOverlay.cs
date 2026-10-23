using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Strange_Universe.Game.Systems;

namespace Strange_Universe.Game.UI;

/// <summary>
/// Draws the docked screen: a picture of the planet's type, its name, the spaceport's description and the menu of services
/// (see <see cref="SpaceportMenu"/>). The pictures are loaded from the Art folder the first time each type is shown, and are released by <see cref="Dispose"/>.
/// Each picture's edges fade into the panel colour (<see cref="SpaceportArt.EdgeFade"/>), so it blends in whatever its own background is.
/// </summary>
public sealed class SpaceportOverlay : IDisposable
{
    private static readonly Color Panel     = new(0, 5, 18);
    private static readonly Color Border    = new(60, 90, 130);
    private static readonly Color Title     = new(255, 255, 255);
    private static readonly Color Body      = new(170, 180, 190);
    private static readonly Color Item      = new(200, 205, 210);
    private static readonly Color Highlight = new(255, 200, 60);
    private static readonly Color Hint      = new(110, 120, 130);

    private const float TitleScale = 1.6f;
    private const int   PanelWidth = 520;
    private const int   StorePanelWidth = 780;
    private const int   Padding    = 28;
    private const int   RowHeight  = 34;
    private const int   ImageGap   = 14;
    private const int   MinImage   = 120;
    private const int   ScreenMargin = 30;

    private readonly RenderService _render;
    private readonly SpriteFont _font;
    private readonly GraphicsDevice _graphicsDevice;
    private readonly Dictionary<PlanetType, Texture2D> _art = new(); // a null value is a picture that could not be loaded
    private Texture2D _fadeMask; // the panel colour, opaque at the edges and clear inside: laid over a picture so it ends in the panel colour

    public SpaceportOverlay(RenderService render, SpriteFont font, GraphicsDevice graphicsDevice)
    {
        _render = render;
        _font = font;
        _graphicsDevice = graphicsDevice;
    }

    public void Draw(SpaceportMenu menu, int screenWidth, int screenHeight)
    {
        _render.Begin(BatchMode.ScreenAlpha);
        var sb = _render.SpriteBatch;

        string title = menu.Planet.Name;
        string description = menu.Planet.Spaceport.Description;
        var lines = menu.Lines;
        string heading = menu.Heading;
        int panelWidth = heading == null ? PanelWidth : StorePanelWidth; // a store lists long lines, so it gets a wider panel
        Vector2 titleSize = _font.MeasureString(title) * TitleScale;
        Vector2 descriptionSize = _font.MeasureString(description);

        int headingHeight = heading == null ? 0 : RowHeight;
        int fixedHeight = Padding * 2 + (int)titleSize.Y + 12 + (int)descriptionSize.Y + 28 + headingHeight + 28 + 20;

        // A long list (a store) shows a window of rows that follows the highlight, and drops the picture to make room.
        int roomForRows = (screenHeight - ScreenMargin * 2 - fixedHeight) / RowHeight;
        int visibleRows = Math.Max(3, Math.Min(lines.Count, heading == null ? lines.Count : roomForRows));
        int firstRow = Math.Clamp(menu.Selected - visibleRows / 2, 0, Math.Max(0, lines.Count - visibleRows));
        int textHeight = fixedHeight + visibleRows * RowHeight;

        // The picture is as wide as the panel's text area, shrunk when the screen is too short to fit it as well.
        Texture2D art = heading == null ? ArtFor(menu.Planet.Type) : null;
        int imageSize = 0;
        if (art != null)
        {
            imageSize = Math.Min(panelWidth - Padding * 2, screenHeight - ScreenMargin * 2 - textHeight - ImageGap);
            if (imageSize < MinImage) imageSize = 0; // no room: leave it out rather than squash the menu
        }

        int panelHeight = textHeight + (imageSize > 0 ? imageSize + ImageGap : 0);
        int left = (screenWidth - panelWidth) / 2;
        int top = (screenHeight - panelHeight) / 2;

        DrawRect(sb, left, top, panelWidth, panelHeight, Panel * 0.95f);
        DrawBorder(sb, left, top, panelWidth, panelHeight, 1, Border);

        string credits = $"Credits: {menu.Credits}";
        Vector2 creditsSize = _font.MeasureString(credits) * 0.8f;
        sb.DrawString(_font, credits, new Vector2(left + panelWidth - creditsSize.X - 14, top + 10), Highlight, 0f, Vector2.Zero, 0.8f, SpriteEffects.None, 0f);

        float y = top + Padding;
        if (imageSize > 0)
        {
            var imageRect = new Rectangle(left + (panelWidth - imageSize) / 2, (int)y, imageSize, imageSize);
            sb.Draw(art, imageRect, Color.White);
            sb.Draw(FadeMask(), imageRect, Color.White); // fades the picture's edges into the panel, whatever its background is
            y += imageSize + ImageGap;
        }

        sb.DrawString(_font, title, new Vector2(left + (panelWidth - titleSize.X) / 2f, y), Title, 0f, Vector2.Zero, TitleScale, SpriteEffects.None, 0f);
        y += titleSize.Y + 12;
        sb.DrawString(_font, description, new Vector2(left + (panelWidth - descriptionSize.X) / 2f, y), Body);
        y += descriptionSize.Y + 28;

        if (heading != null)
        {
            sb.DrawString(_font, heading, new Vector2(left + Padding, y), Title);
            y += RowHeight;
        }

        for (int i = firstRow; i < firstRow + visibleRows && i < lines.Count; i++)
        {
            bool selected = i == menu.Selected;
            string text = (selected ? "> " : "  ") + lines[i];
            if (selected)
                DrawRect(sb, left + Padding - 8, (int)y - 4, panelWidth - (Padding - 8) * 2, RowHeight - 4, Highlight * 0.12f);
            sb.DrawString(_font, text, new Vector2(left + Padding, y), selected ? Highlight : Item);
            y += RowHeight;
        }

        string hint = heading == null ? "Up/Down: choose     Enter: select     Esc: menu" : "Up/Down: choose     Enter: buy or sell     Esc: back";
        Vector2 hintSize = _font.MeasureString(hint) * 0.7f;
        sb.DrawString(_font, hint, new Vector2(left + (panelWidth - hintSize.X) / 2f, top + panelHeight - Padding - hintSize.Y + 8),
                      Hint, 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);
    }

    private const int FadeMaskSize = 128;

    /// <summary>
    /// A small square texture of the panel colour (premultiplied) whose opacity is <see cref="SpaceportArt.EdgeFade"/>. Stretched over a picture it
    /// leaves the middle untouched and blends the edges into the panel.
    /// </summary>
    private Texture2D FadeMask()
    {
        if (_fadeMask != null) return _fadeMask;

        var pixels = new Color[FadeMaskSize * FadeMaskSize];
        for (int py = 0; py < FadeMaskSize; py++)
        {
            for (int px = 0; px < FadeMaskSize; px++)
            {
                float alpha = SpaceportArt.EdgeFade((px + 0.5f) / FadeMaskSize, (py + 0.5f) / FadeMaskSize);
                pixels[py * FadeMaskSize + px] = Panel * alpha; // premultiplied: colour and opacity scale together
            }
        }

        _fadeMask = new Texture2D(_graphicsDevice, FadeMaskSize, FadeMaskSize);
        _fadeMask.SetData(pixels);
        return _fadeMask;
    }

    /// <summary>The picture for a planet type, loaded on first use. Null when the file is missing (the screen is then drawn without one).</summary>
    private Texture2D ArtFor(PlanetType type)
    {
        if (!_art.TryGetValue(type, out var texture))
        {
            string path = SpaceportArt.PathFor(type);
            texture = path == null ? null : ArtLoader.TryLoad(_graphicsDevice, path);
            _art[type] = texture;
        }
        return texture;
    }

    public void Dispose()
    {
        foreach (var texture in _art.Values)
            texture?.Dispose();
        _art.Clear();
        _fadeMask?.Dispose();
        _fadeMask = null;
    }

    private void DrawRect(SpriteBatch sb, int x, int y, int w, int h, Color color) =>
        sb.Draw(_render.Pixel, new Rectangle(x, y, w, h), color);

    private void DrawBorder(SpriteBatch sb, int x, int y, int w, int h, int t, Color color)
    {
        DrawRect(sb, x, y, w, t, color);
        DrawRect(sb, x, y + h - t, w, t, color);
        DrawRect(sb, x, y, t, h, color);
        DrawRect(sb, x + w - t, y, t, h, color);
    }
}
