using Microsoft.Xna.Framework;
using Strange_Universe.Game.Components;
using Strange_Universe.Game.Entities;
using Strange_Universe.Game.Systems;
using Strange_Universe.Game.UI;

namespace Strange_Universe.Game.Screens;

/// <summary>
/// Active gameplay. Owns all per-session resources: created in <see cref="OnEnter"/>,
/// persisted and disposed in <see cref="OnExit"/> (also the path used on shutdown).
/// </summary>
public sealed class PlayingScreen : IScreen
{
    private readonly ScreenContext _ctx;
    private readonly ProjectileRenderer _projectileRenderer;
    private readonly Universe _universe;
    private readonly HudMessageService _hud = new();

    private GameServices _services;
    private Camera _camera;
    private InputHandler _input;
    private SpriteRenderer _sprites;
    private WorldRenderer _world;
    private HudRenderer _hudRenderer;
    private GalaxyMapOverlay _galaxyMap;

    public PlayingScreen(ScreenContext ctx, ProjectileRenderer projectileRenderer, Universe universe)
    {
        _ctx = ctx;
        _projectileRenderer = projectileRenderer;
        _universe = universe;
    }

    public void OnEnter()
    {
        _services = new GameServices(_ctx.GraphicsDevice);
        _camera = new Camera(_ctx.ScreenWidth, _ctx.ScreenHeight);
        _input = _ctx.Input;
        _input.Rebaseline();
        _sprites = new SpriteRenderer(_ctx.Font, _services, _ctx.Render, _camera);
        _world = new WorldRenderer(_sprites, _projectileRenderer,
            new DebugRenderer(_ctx.Render, _camera, _ctx.Settings), _ctx.Settings);
        _hudRenderer = new HudRenderer(_ctx.Render, _ctx.Font);
        _galaxyMap = new GalaxyMapOverlay(_ctx.Font, _ctx.GraphicsDevice, _ctx.Render);

        _universe.Settings = _ctx.Settings;
        _universe.Generate(_services.Assets, _hud);
        _sprites.ImpactEffects.Attach(_universe.ActiveStarSystem);
        _ctx.SetMouseVisible(false);
    }

    public void OnExit()
    {
        _ctx.Saves.Save(_universe);
        _universe.Dispose();
        _galaxyMap?.Dispose();
        _services?.Dispose();
        _galaxyMap = null;
        _services = null;
    }

    public void Update(GameTime gameTime)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        // Galaxy map intercepts all input while open.
        if (_galaxyMap.IsOpen)
        {
            if (_galaxyMap.Update(_universe, _ctx.ScreenWidth, _ctx.ScreenHeight, dt))
            {
                _ctx.SetMouseVisible(false);
                _input.Rebaseline(); // so the closing Escape is not seen as Exit
            }
            return;
        }

        var input = _input.GetState();
        if (input.Exit) { _ctx.ShowMenu(); return; }
        if (input.OpenMap)
        {
            _galaxyMap.Open();
            _ctx.SetMouseVisible(true);
            return;
        }

        _hud.Update(dt);
        // Age existing hit visuals before this frame's impacts, so new hits draw at full life.
        _sprites.ImpactEffects.Update(dt);

        _universe.Update(dt, input);
        _camera.Update(_universe.Player.CameraTarget, dt, input);

        // Hit visuals follow the active system (re-subscribes after a jump).
        _sprites.ImpactEffects.Attach(_universe.ActiveStarSystem);
        _projectileRenderer.UpdateParticles(_universe.ActiveStarSystem.Projectiles, dt);
    }

    public void Draw(GameTime gameTime)
    {
        _ctx.GraphicsDevice.Clear(ScreenContext.Background);
        _world.Draw(_universe.ActiveStarSystem, _camera);
        DrawOverlay();

        if (_galaxyMap.IsOpen)
            _galaxyMap.Draw(_universe, _ctx.ScreenWidth, _ctx.ScreenHeight);
    }

    private void DrawOverlay()
    {
        int w = _ctx.ScreenWidth, h = _ctx.ScreenHeight;
        _ctx.Render.Begin(BatchMode.ScreenAlpha);
        _sprites.DrawSpeedBar(_universe.Player, w, h, _universe.Player.MaxSpeed);
        _sprites.DrawHud(_universe, w, h);

        _hudRenderer.DrawMessage(_hud, w, h);
    }
}
