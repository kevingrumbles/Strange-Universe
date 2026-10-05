using Strange_Universe.Game.Components;
using Strange_Universe.Game.Entities;

namespace Strange_Universe.Game.Systems;

/// <summary>
/// Draws one star system in fixed layer order. All batches go through <see cref="RenderService.Begin"/>
/// (via the sub-renderers), so RenderService stays the single batch owner.
/// </summary>
public sealed class WorldRenderer
{
    private readonly SpriteRenderer _sprites;
    private readonly ProjectileRenderer _projectiles;
    private readonly DebugRenderer _debug;
    private readonly GameSettings _settings;
    private readonly int _screenWidth;
    private readonly int _screenHeight;

    public WorldRenderer(SpriteRenderer sprites, ProjectileRenderer projectiles, DebugRenderer debug,
                         GameSettings settings, int screenWidth, int screenHeight)
    {
        _sprites = sprites;
        _projectiles = projectiles;
        _debug = debug;
        _settings = settings;
        _screenWidth = screenWidth;
        _screenHeight = screenHeight;
    }

    public void Draw(StarSystem sys, Camera camera, Player player)
    {
        _sprites.DrawBackgroundStars(sys.BackgroundStars, _screenWidth, _screenHeight, camera.Position, camera.Zoom, layer: 0);
        _sprites.DrawNebula(sys.NebulaId, _screenWidth, _screenHeight, camera.Position, camera.Zoom, _settings.ShowDebug);
        _sprites.DrawBackgroundStars(sys.BackgroundStars, _screenWidth, _screenHeight, camera.Position, camera.Zoom, layer: 1);

        foreach (var star in sys.Stars) _sprites.DrawStar(star);
        foreach (var planet in sys.Planets) _sprites.DrawPlanet(planet);
        foreach (var asteroid in sys.Asteroids) _sprites.DrawAsteroid(asteroid);

        _sprites.DrawPlayer(player);
        foreach (var npc in sys.Npcs) _sprites.DrawNPC(npc);

        _projectiles.SetCameraMatrix(camera.GetTransformMatrix());
        _projectiles.DrawAll(sys.Projectiles);

        _debug.Draw(sys, player);
    }
}
