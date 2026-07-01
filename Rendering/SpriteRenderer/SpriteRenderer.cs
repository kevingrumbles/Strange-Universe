using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StrangeUniverse.Game.Entities;
using StrangeUniverse.Game.World;
using StrangeUniverse.Rendering.Camera;
using StrangeUniverse.Rendering.ProceduralGeneration;

namespace StrangeUniverse.Rendering.SpriteRenderer;

/// <summary>
/// All SpriteBatch draw calls are funnelled through this class.
/// Game logic never calls any drawing API directly.
/// </summary>
public class SpriteRenderer
{
    private readonly SpriteBatch           _spriteBatch;
    private readonly ProceduralTextureCache _cache;
    private readonly Texture2D             _pixel;      // 1×1 white texture for dots

    public SpriteRenderer(SpriteBatch spriteBatch, GraphicsDevice gd, ProceduralTextureCache cache)
    {
        _spriteBatch = spriteBatch;
        _cache       = cache;

        _pixel = new Texture2D(gd, 1, 1);
        _pixel.SetData(new[] { Color.White });
    }

    // ── World-space pass (SpriteBatch already began with camera matrix) ──────

    public void DrawNebula(string textureId, int screenWidth, int screenHeight, Vector2 cameraPos)
    {
        if (!_cache.TryGet(textureId, out var tex) || tex is null) return;

        // Tile the nebula across the screen at a slow parallax rate
        float parallax = 0.05f;
        float ox = (cameraPos.X * parallax) % tex.Width;
        float oy = (cameraPos.Y * parallax) % tex.Height;

        // Draw 2×2 tiles to ensure full coverage after offset
        for (int tx = -1; tx <= 1; tx++)
        for (int ty = -1; ty <= 1; ty++)
        {
            var dest = new Rectangle(
                (int)(-ox + tx * tex.Width  - screenWidth  / 2),
                (int)(-oy + ty * tex.Height - screenHeight / 2),
                tex.Width, tex.Height);
            _spriteBatch.Draw(tex, dest, Color.White * 0.55f);
        }
    }

    public void DrawBackgroundStars(IReadOnlyList<BackgroundStar> stars,
                                     int screenWidth, int screenHeight, Vector2 cameraPos)
    {
        float parallax = 0.08f;
        foreach (var star in stars)
        {
            // Screen-space parallax wrapping
            float sx = ((star.Position.X - cameraPos.X * parallax) % screenWidth  + screenWidth)  % screenWidth;
            float sy = ((star.Position.Y - cameraPos.Y * parallax) % screenHeight + screenHeight) % screenHeight;

            // Stars are drawn in screen space — offset by camera's top-left corner in world coords
            // to cancel the camera transform applied by SpriteBatch
            Vector2 worldPos = new Vector2(
                cameraPos.X - screenWidth  / 2f + sx,
                cameraPos.Y - screenHeight / 2f + sy);

            byte bright = (byte)(star.Brightness * 255f);
            int  size   = (int)Math.Max(1, star.Size);
            _spriteBatch.Draw(_pixel,
                new Rectangle((int)worldPos.X, (int)worldPos.Y, size, size),
                new Color(bright, bright, bright));
        }
    }

    /// <summary>Draws any entity that has a position, rotation, scale, and texture ID.</summary>
    public void DrawEntity(string textureId, Vector2 position, float rotation, float radius)
    {
        if (!_cache.TryGet(textureId, out var tex) || tex is null) return;

        float scale = radius * 2f / Math.Max(tex.Width, tex.Height);
        var origin  = new Vector2(tex.Width * 0.5f, tex.Height * 0.5f);

        _spriteBatch.Draw(tex, position, null, Color.White,
            rotation, origin, scale, SpriteEffects.None, 0f);
    }

    public void DrawStar(Star star) =>
        DrawEntity(star.TextureId, star.Transform.Position, 0f, star.Radius);

    public void DrawPlanet(Planet planet) =>
        DrawEntity(planet.TextureId, planet.Transform.Position, planet.Transform.Rotation, planet.Radius);

    public void DrawAsteroid(Asteroid asteroid) =>
        DrawEntity(asteroid.TextureId, asteroid.Transform.Position, asteroid.Transform.Rotation, asteroid.Radius);

    public void DrawPlayer(PlayerShip player) =>
        DrawEntity(player.TextureId, player.Transform.Position, player.Transform.Rotation, player.Radius * 2.2f);

    // ── HUD pass (no camera transform) ───────────────────────────────────────

    public void DrawHud(PlayerShip player, int screenWidth, int screenHeight)
    {
        // Speed bar in bottom-left
        float speed     = player.Physics.Velocity.Length();
        float maxSpeed  = 550f;
        float barW      = 140;
        float barH      = 8;
        float barX      = 14;
        float barY      = screenHeight - 26f;

        _spriteBatch.Draw(_pixel, new Rectangle((int)barX, (int)barY, (int)barW, (int)barH),
            Color.White * 0.2f);
        float fill = Math.Min(1f, speed / maxSpeed);
        var fillColor = Color.Lerp(new Color(60, 200, 80), new Color(255, 80, 40), fill);
        _spriteBatch.Draw(_pixel,
            new Rectangle((int)barX, (int)barY, (int)(barW * fill), (int)barH),
            fillColor * 0.8f);
    }
}
