using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Strange_Universe.Game.Components;
using Strange_Universe.Game.Entities;
using System;

namespace Strange_Universe.Game.Systems;

/// <summary>World-space debug overlay (gravity wells, hitboxes). Drawn only when <see cref="GameSettings.ShowDebug"/> is set.</summary>
public sealed class DebugRenderer
{
    private readonly RenderService _renderService;
    private readonly Camera _camera;
    private readonly GameSettings _settings;

    public DebugRenderer(RenderService renderService, Camera camera, GameSettings settings)
    {
        _renderService = renderService;
        _camera = camera;
        _settings = settings;
    }

    public void Draw(StarSystem sys, Player player)
    {
        if (!_settings.ShowDebug) return;

        _renderService.Begin(BatchMode.WorldAlpha, _camera.GetTransformMatrix());

        foreach (var star in sys.Stars)
            DrawCircle(star.GravityWell.Center, star.GravityWell.Radius, Color.Yellow * 0.3f, 64);

        foreach (var planet in sys.Planets)
            DrawCircle(planet.GravityWell.Center, planet.GravityWell.Radius, Color.Cyan * 0.3f, 48);

        foreach (var asteroid in sys.Asteroids)
            if (!asteroid.IsDestroyed)
                DrawCircle(asteroid.Position, asteroid.Radius, Color.Orange * 0.5f, 16);

        if (player != null)
            DrawCircle(player.Position, player.Radius, Color.LimeGreen * 0.6f, 24);

        foreach (var npc in sys.Npcs)
            DrawCircle(npc.Position, npc.Radius, Color.Red * 0.6f, 24);
    }

    private void DrawCircle(Vector2 center, float radius, Color color, int segments)
    {
        float step = MathHelper.TwoPi / segments;
        for (int i = 0; i < segments; i++)
        {
            float a1 = i * step, a2 = (i + 1) * step;
            DrawLine(center + new Vector2(MathF.Cos(a1), MathF.Sin(a1)) * radius,
                     center + new Vector2(MathF.Cos(a2), MathF.Sin(a2)) * radius, color, 2f);
        }
    }

    private void DrawLine(Vector2 start, Vector2 end, Color color, float thickness)
    {
        Vector2 edge = end - start;
        _renderService.SpriteBatch.Draw(_renderService.Pixel,
            new Rectangle((int)start.X, (int)start.Y, (int)edge.Length(), (int)thickness),
            null, color, MathF.Atan2(edge.Y, edge.X), new Vector2(0, 0.5f), SpriteEffects.None, 0);
    }
}
