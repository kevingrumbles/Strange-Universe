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
    private readonly SpriteFont            _font;       // Font for HUD text

    public SpriteRenderer(SpriteBatch spriteBatch, GraphicsDevice gd, ProceduralTextureCache cache, SpriteFont font)
    {
        _spriteBatch = spriteBatch;
        _cache       = cache;
        _font        = font;

        _pixel = new Texture2D(gd, 1, 1);
        _pixel.SetData(new[] { Color.White });
    }

    // ── World-space pass (SpriteBatch already began with camera matrix) ──────

    /// <summary>
    /// Draws an infinite scrolling nebula background using a seamless tileable texture.
    /// The nebula appears endless in all directions with parallax scrolling.
    /// </summary>
    public void DrawNebula(string nebulaId, int screenWidth, int screenHeight, 
                          Vector2 cameraPos, float cameraZoom, bool debugMode = false)
    {
        if (!_cache.TryGet(nebulaId, out var tex) || tex is null) return;

        int textureSize = tex.Width; // Should be 4096 from Nebula.Size

        // Apply parallax (slower movement than camera for depth)
        const float parallax = StrangeUniverse.Game.Entities.Nebula.ParallaxFactor;
        float parallaxOffsetX = cameraPos.X * parallax;
        float parallaxOffsetY = cameraPos.Y * parallax;

        // Calculate how large the texture should appear in screen space
        float screenTextureSize = textureSize * 2.0f; // Each tile covers this many screen pixels

        // Calculate which tiles we need to draw to cover the screen
        int startTileX = (int)Math.Floor(parallaxOffsetX / screenTextureSize);
        int endTileX = (int)Math.Ceiling((parallaxOffsetX + screenWidth) / screenTextureSize);
        int startTileY = (int)Math.Floor(parallaxOffsetY / screenTextureSize);
        int endTileY = (int)Math.Ceiling((parallaxOffsetY + screenHeight) / screenTextureSize);

        // Draw tiles to cover the screen
        for (int tileX = startTileX; tileX <= endTileX; tileX++)
        {
            for (int tileY = startTileY; tileY <= endTileY; tileY++)
            {
                // Calculate screen-space position for this tile
                float screenX = tileX * screenTextureSize - parallaxOffsetX;
                float screenY = tileY * screenTextureSize - parallaxOffsetY;

                // Convert screen position to world position for camera transform
                // The camera transform will be applied by SpriteBatch, so we need to
                // "undo" it by calculating where in world space this screen pixel maps to
                float worldX = cameraPos.X - (screenWidth / 2f / cameraZoom) + (screenX / cameraZoom);
                float worldY = cameraPos.Y - (screenHeight / 2f / cameraZoom) + (screenY / cameraZoom);

                // World size for this tile (compensate for zoom)
                float worldTileSize = screenTextureSize / cameraZoom;

                _spriteBatch.Draw(tex,
                    new Rectangle((int)worldX, (int)worldY, 
                                 (int)worldTileSize, (int)worldTileSize),
                    null, // Use entire texture
                    Color.White,
                    0f,
                    Vector2.Zero,
                    SpriteEffects.None,
                    0f);
            }
        }
    }

    public void DrawBackgroundStars(IReadOnlyList<BackgroundStar> stars,
                                     int screenWidth, int screenHeight, Vector2 cameraPos,
                                     float cameraZoom, int layer)
    {
        // Background stars render in screen space and maintain constant appearance
        // regardless of zoom level. They provide atmosphere without cluttering the view.

        // Define the base tile size for background stars (matches typical screen resolution)
        const float tileWidth = 1920f;
        const float tileHeight = 1080f;

        float parallax = 0.08f;

        // Apply parallax to camera position for depth effect
        float parallaxOffsetX = cameraPos.X * parallax;
        float parallaxOffsetY = cameraPos.Y * parallax;

        // Calculate which tiles to draw based on screen space with parallax
        // We always tile to cover the screen, not the world
        int startTileX = (int)Math.Floor(parallaxOffsetX / tileWidth);
        int endTileX = (int)Math.Ceiling((parallaxOffsetX + screenWidth) / tileWidth);
        int startTileY = (int)Math.Floor(parallaxOffsetY / tileHeight);
        int endTileY = (int)Math.Ceiling((parallaxOffsetY + screenHeight) / tileHeight);

        // Draw stars in each tile needed to cover the screen
        for (int tileX = startTileX; tileX <= endTileX; tileX++)
        {
            for (int tileY = startTileY; tileY <= endTileY; tileY++)
            {
                // Calculate tile offset
                float tileOffsetX = tileX * tileWidth;
                float tileOffsetY = tileY * tileHeight;

                // Mirror tiles for visual variety (checkerboard pattern)
                bool mirrorX = (tileX % 2) != 0;
                bool mirrorY = (tileY % 2) != 0;

                foreach (var star in stars)
                {
                    if (star.Layer != layer) continue;

                    // Calculate star position within the tile
                    float starX = star.Position.X;
                    float starY = star.Position.Y;

                    // Apply mirroring
                    if (mirrorX) starX = tileWidth - starX;
                    if (mirrorY) starY = tileHeight - starY;

                    // Calculate screen-space position with parallax
                    float screenX = tileOffsetX + starX - parallaxOffsetX;
                    float screenY = tileOffsetY + starY - parallaxOffsetY;

                    // Convert screen position to world position to work with camera transform
                    // The camera transform will be applied by SpriteBatch, so we need to
                    // "undo" it by calculating where in world space this screen pixel maps to
                    float worldX = cameraPos.X - (screenWidth / 2f / cameraZoom) + (screenX / cameraZoom);
                    float worldY = cameraPos.Y - (screenHeight / 2f / cameraZoom) + (screenY / cameraZoom);

                    Vector2 worldPos = new Vector2(worldX, worldY);

                    byte bright = (byte)(star.Brightness * 255f);

                    // Keep star size constant in screen space
                    float worldSize = star.Size / cameraZoom;
                    int size = (int)Math.Max(1, worldSize);

                    _spriteBatch.Draw(_pixel,
                        new Rectangle((int)worldPos.X, (int)worldPos.Y, size, size),
                        new Color(bright, bright, bright));
                }
            }
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
                   player.Transform.Rotation + player.Ship.SpriteRotationOffset,
                   player.Radius * 2.2f * player.Ship.SpriteScale);

    /// <summary>
    /// Draws a circle outline (ring) for debug visualization.
    /// Uses line segments to approximate a circle.
    /// </summary>
    public void DrawDebugCircle(Vector2 center, float radius, Color color, int segments = 32)
    {
        float angleStep = MathHelper.TwoPi / segments;
        for (int i = 0; i < segments; i++)
        {
            float angle1 = i * angleStep;
            float angle2 = (i + 1) * angleStep;

            Vector2 p1 = center + new Vector2(
                MathF.Cos(angle1) * radius,
                MathF.Sin(angle1) * radius);
            Vector2 p2 = center + new Vector2(
                MathF.Cos(angle2) * radius,
                MathF.Sin(angle2) * radius);

            DrawLine(p1, p2, color, 2f);
        }
    }

    /// <summary>
    /// Draws a line between two points using a stretched pixel.
    /// </summary>
    private void DrawLine(Vector2 start, Vector2 end, Color color, float thickness)
    {
        Vector2 edge = end - start;
        float length = edge.Length();
        float angle = MathF.Atan2(edge.Y, edge.X);

        _spriteBatch.Draw(_pixel,
            new Rectangle((int)start.X, (int)start.Y, (int)length, (int)thickness),
            null,
            color,
            angle,
            new Vector2(0, 0.5f),
            SpriteEffects.None,
            0);
    }

    // ── HUD pass (no camera transform) ───────────────────────────────────────

    public void DrawSpeedBar(Player player, int screenWidth, int screenHeight, float maxSpeed)
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

    // ── HUD ───────────────────────────────────────────────────────────────

    public void DrawHud(Universe universe, int screenWidth, int screenHeight)
    {
        const int MapSize = 180;
        const int Margin  = 14;
        const int Border  = 1;

        int   mapLeft   = screenWidth - MapSize - Margin;
        int   mapTop    = Margin;
        int   hudPanelHeight = screenHeight - (Margin * 2); // Extend to bottom with same margin
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

        // ── HUD Panel Background ─────────────────────────────────────────────
        _spriteBatch.Draw(_pixel,
            new Rectangle(mapLeft, mapTop, MapSize, hudPanelHeight),
            new Color(0, 5, 18) * 0.84f);

        // ── Minimap Background (darker inset within panel) ──────────────────
        _spriteBatch.Draw(_pixel,
            new Rectangle(mapLeft, mapTop, MapSize, MapSize),
            new Color(0, 5, 18) * 0.95f);

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

        // ── Minimap Border (drawn to separate minimap from HUD info) ────
        Color border = Color.White * 0.30f;
        _spriteBatch.Draw(_pixel, new Rectangle(mapLeft,              mapTop,                    MapSize, Border),  border);
        _spriteBatch.Draw(_pixel, new Rectangle(mapLeft,              mapTop + MapSize - Border, MapSize, Border),  border);
        _spriteBatch.Draw(_pixel, new Rectangle(mapLeft,              mapTop,                    Border,  MapSize), border);
        _spriteBatch.Draw(_pixel, new Rectangle(mapLeft + MapSize - Border, mapTop,              Border,  MapSize), border);

        // ── HUD Panel Border (outer border for entire panel) ────────────
        _spriteBatch.Draw(_pixel, new Rectangle(mapLeft,                      mapTop,                             MapSize, Border),           border);
        _spriteBatch.Draw(_pixel, new Rectangle(mapLeft,                      mapTop + hudPanelHeight - Border,   MapSize, Border),           border);
        _spriteBatch.Draw(_pixel, new Rectangle(mapLeft,                      mapTop,                             Border,  hudPanelHeight),   border);
        _spriteBatch.Draw(_pixel, new Rectangle(mapLeft + MapSize - Border,   mapTop,                             Border,  hudPanelHeight),   border);

        // ── Jump Target Display ──────────────────────────────────────────
        // Display jump target system name below the minimap
        if (universe.JumpRoute.Count > 0)
        {
            string targetSystemId = universe.JumpRoute[0];
            var targetNode = universe.StarSystemNodes.Find(n => n.SystemId == targetSystemId);

            if (targetNode != null)
            {
                string displayName = targetNode.DisplayName ?? "Undiscovered";

                // Check if player is in a gravity well
                bool inGravityWell = universe.ActiveStarSystem.CalculateGravityAtLocation(universe.Player.Transform.Position) != Vector2.Zero;

                // Set text color based on gravity well status
                Color textColor = inGravityWell 
                    ? new Color(80, 80, 80)      // Grey when in gravity well
                    : new Color(220, 220, 220);  // Bright when jump available

                // Draw system name centered below minimap
                Vector2 nameSize = _font.MeasureString(displayName);
                Vector2 namePos = new Vector2(
                    mapLeft + (MapSize - nameSize.X) * 0.5f,
                    mapTop + MapSize + 15);
                _spriteBatch.DrawString(_font, displayName, namePos, textColor);
            }
        }
        else
        {
            // No jump target set - display "Nav System Off"
            string noTarget = "Nav System Off";
            Vector2 textSize = _font.MeasureString(noTarget);
            Vector2 textPos = new Vector2(
                mapLeft + (MapSize - textSize.X) * 0.5f,
                mapTop + MapSize + 15);
            _spriteBatch.DrawString(_font, noTarget, textPos, new Color(100, 100, 100));
        }
    }
}
