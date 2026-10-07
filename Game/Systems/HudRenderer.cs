using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Strange_Universe.Game.UI;

namespace Strange_Universe.Game.Systems;

/// <summary>Draws the timed on-screen message. Expects a screen-space batch to be active or begins one.</summary>
public sealed class HudRenderer
{
    private static readonly Color MessageColor = new(255, 140, 0);

    private readonly RenderService _render;
    private readonly SpriteFont _font;

    public HudRenderer(RenderService render, SpriteFont font)
    {
        _render = render;
        _font = font;
    }

    /// <summary>Draws the current message centred near the bottom, fading out over its last second.</summary>
    public void DrawMessage(HudMessageService hud, int screenWidth, int screenHeight)
    {
        if (hud.Remaining <= 0f || string.IsNullOrEmpty(hud.Message)) return;

        float alpha = hud.Remaining < 1f ? hud.Remaining : 1f;
        Vector2 size = _font.MeasureString(hud.Message);
        _render.SpriteBatch.DrawString(_font, hud.Message,
            new Vector2((screenWidth - size.X) / 2f, screenHeight - size.Y - 40f), MessageColor * alpha);
    }
}
