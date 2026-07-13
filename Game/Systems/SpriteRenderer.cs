using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Strange_Universe.Game.Entities;
using StrangeUniverse;
using StrangeUniverse.Game.Entities;
using System;
using System.Collections.Generic;

namespace Strange_Universe.Game.Systems;

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

    /// <summary>
    /// Draws the nebula as a single large rectangle in world space.
    /// Because SpriteBatch has the camera transform applied, only the
    /// visible portion renders each frame — no tiling or screen-space tricks.
    /// </summary>
    public void DrawNebula(Nebula nebula, float worldSize)
    {
        if (!_cache.TryGet(nebula.Id, out var tex) || tex is null) return;

        int half = (int)(worldSize * 0.5f);
        _spriteBatch.Draw(tex,
            new Rectangle(-half, -half, (int)worldSize, (int)worldSize),
            Color.White);
    }

    public void DrawBackgroundStars(IReadOnlyList<BackgroundStar> stars,
                                     int screenWidth, int screenHeight, Vector2 cameraPos,
                                     int layer)
    {
        float parallax = 0.08f;
        foreach (var star in stars)
        {
            if (star.Layer != layer) continue;

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
        DrawEntity(star.Id, star.Transform.Position, 0f, star.Radius);

    public void DrawPlanet(Planet planet) =>
        DrawEntity(planet.Id, planet.Transform.Position, planet.Transform.Rotation, planet.Radius);

    public void DrawAsteroid(Asteroid asteroid) =>
        DrawEntity(asteroid.TextureId, asteroid.Transform.Position, asteroid.Transform.Rotation, asteroid.Radius);

    public void DrawPlayer(Player player) =>
        DrawEntity(player.ShipName, player.Transform.Position,
                   player.Transform.Rotation + player.SpriteRotationOffset,
                   player.Radius * 2.2f);

    // ── HUD pass (no camera transform) ───────────────────────────────────────

    public void DrawHud(Player player, int screenWidth, int screenHeight, float maxSpeed)
    {
        // Speed bar in bottom-left
        float speed     = player.Physics.Velocity.Length();
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

    // ── Minimap ───────────────────────────────────────────────────────────────

    public void DrawMinimap(Universe universe, int screenWidth, int screenHeight)
    {
        const int MapSize = 180;
        const int Margin  = 14;
        const int Border  = 1;

        int   mapLeft   = screenWidth - MapSize - Margin;
        int   mapTop    = Margin;
        float halfMap   = MapSize * 0.5f;
        float scale     = halfMap / universe.ActiveStarSystem.SystemRadius;   // world unit → minimap pixel

        // ── Local helpers ────────────────────────────────────────────────────

        // Converts a world-space position to a screen-space position on the minimap.
        Vector2 WorldToMap(Vector2 world) => new(
            mapLeft + halfMap + world.X * scale,
            mapTop  + halfMap + world.Y * scale);

        // Draws a square dot centred on screenPos, clipped to the map bounds.
        void Dot(Vector2 screenPos, int size, Color color)
        {
            int x = (int)screenPos.X - size / 2;
            int y = (int)screenPos.Y - size / 2;
            if (x + size <= mapLeft || x >= mapLeft + MapSize) return;
            if (y + size <= mapTop  || y >= mapTop  + MapSize) return;
            _spriteBatch.Draw(_pixel, new Rectangle(x, y, size, size), color);
        }

        // ── Background ───────────────────────────────────────────────────────
        _spriteBatch.Draw(_pixel,
            new Rectangle(mapLeft, mapTop, MapSize, MapSize),
            new Color(0, 5, 18) * 0.84f);

        // ── Asteroids (drawn first — smallest, dimmest) ───────────────────
        foreach (var asteroid in universe.ActiveStarSystem.Asteroids)
            Dot(WorldToMap(asteroid.Transform.Position), 1, new Color(85, 85, 90, 170));

        // ── Planets ───────────────────────────────────────────────────────
        foreach (var planet in universe.ActiveStarSystem.Planets)
            Dot(WorldToMap(planet.Transform.Position), 4, StaticHelpers.PlanetMinimapColor(planet.Type));

        // Stars
        foreach (var star in universe.ActiveStarSystem.Stars)
        {
            Vector2 starMap = WorldToMap(star.Transform.Position);
            Dot(starMap, 10, star.MinimapColor * 0.55f);   // soft outer glow
            Dot(starMap,  6, star.MinimapColor);            // coloured body
            Dot(starMap,  3, Color.White * 0.90f);          // bright core
        }

        // ── Player ────────────────────────────────────────────────────────
        Vector2 playerMap = WorldToMap(universe.Player.Transform.Position);
        Dot(playerMap, 4, new Color(55, 215, 255));                // cyan body

        // Heading pip — white dot ahead of the player indicating facing direction
        Vector2 pip = playerMap + universe.Player.Transform.Forward * 5f;
        Dot(pip, 2, Color.White);

        // ── Border (drawn last to cleanly cap any dot bleed) ─────────────
        Color border = Color.White * 0.30f;
        _spriteBatch.Draw(_pixel, new Rectangle(mapLeft,              mapTop,                    MapSize, Border),  border);
        _spriteBatch.Draw(_pixel, new Rectangle(mapLeft,              mapTop + MapSize - Border, MapSize, Border),  border);
        _spriteBatch.Draw(_pixel, new Rectangle(mapLeft,              mapTop,                    Border,  MapSize), border);
        _spriteBatch.Draw(_pixel, new Rectangle(mapLeft + MapSize - Border, mapTop,              Border,  MapSize), border);
    }
}
