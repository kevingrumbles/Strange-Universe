using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Strange_Universe.Game.Systems;

namespace Strange_Universe.Game.UI;

/// <summary>Draws the docked screen: the planet's name, the spaceport's description and the menu of services (see <see cref="SpaceportMenu"/>).</summary>
public sealed class SpaceportOverlay
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
    private const int   Padding    = 28;
    private const int   RowHeight  = 34;

    private readonly RenderService _render;
    private readonly SpriteFont _font;

    public SpaceportOverlay(RenderService render, SpriteFont font)
    {
        _render = render;
        _font = font;
    }

    public void Draw(SpaceportMenu menu, int screenWidth, int screenHeight)
    {
        _render.Begin(BatchMode.ScreenAlpha);
        var sb = _render.SpriteBatch;

        string title = menu.Planet.Name;
        string description = menu.Planet.Spaceport.Description;
        Vector2 titleSize = _font.MeasureString(title) * TitleScale;
        Vector2 descriptionSize = _font.MeasureString(description);

        int panelHeight = Padding * 2 + (int)titleSize.Y + 12 + (int)descriptionSize.Y + 28 + menu.Items.Count * RowHeight + 28 + 20;
        int left = (screenWidth - PanelWidth) / 2;
        int top = (screenHeight - panelHeight) / 2;

        DrawRect(sb, left, top, PanelWidth, panelHeight, Panel * 0.95f);
        DrawBorder(sb, left, top, PanelWidth, panelHeight, 1, Border);

        float y = top + Padding;
        sb.DrawString(_font, title, new Vector2(left + (PanelWidth - titleSize.X) / 2f, y), Title, 0f, Vector2.Zero, TitleScale, SpriteEffects.None, 0f);
        y += titleSize.Y + 12;
        sb.DrawString(_font, description, new Vector2(left + (PanelWidth - descriptionSize.X) / 2f, y), Body);
        y += descriptionSize.Y + 28;

        for (int i = 0; i < menu.Items.Count; i++)
        {
            bool selected = i == menu.Selected;
            string text = (selected ? "> " : "  ") + menu.Items[i].Name;
            if (selected)
                DrawRect(sb, left + Padding - 8, (int)y - 4, PanelWidth - (Padding - 8) * 2, RowHeight - 4, Highlight * 0.12f);
            sb.DrawString(_font, text, new Vector2(left + Padding, y), selected ? Highlight : Item);
            y += RowHeight;
        }

        const string hint = "Up/Down: choose     Enter: select     Esc: menu";
        Vector2 hintSize = _font.MeasureString(hint) * 0.7f;
        sb.DrawString(_font, hint, new Vector2(left + (PanelWidth - hintSize.X) / 2f, top + panelHeight - Padding - hintSize.Y + 8),
                      Hint, 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);
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
