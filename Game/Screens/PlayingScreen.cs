using System;
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

    private AssetService _assets;
    private Camera _camera;
    private InputHandler _input;
    private SpriteRenderer _sprites;
    private DebugRenderer _debug;
    private GalaxyMapOverlay _galaxyMap;
    private SpaceportOverlay _spaceportOverlay;
    private SpaceportMenu _spaceportMenu;
    private IDisposable _dockSaveSubscription;
    private IDisposable _hudSubscription;

    public PlayingScreen(ScreenContext ctx, ProjectileRenderer projectileRenderer, Universe universe)
    {
        _ctx = ctx;
        _projectileRenderer = projectileRenderer;
        _universe = universe;
    }

    public void OnEnter()
    {
        _assets = new AssetService(_ctx.GraphicsDevice);
        _camera = new Camera(_ctx.ScreenWidth, _ctx.ScreenHeight);
        _input = _ctx.Input;
        _input.Rebaseline();
        _sprites = new SpriteRenderer(_ctx.Font, _assets, _ctx.Render, _camera);
        _debug = new DebugRenderer(_ctx.Render, _camera, _ctx.Settings);
        _galaxyMap = new GalaxyMapOverlay(_ctx.Font, _ctx.GraphicsDevice, _ctx.Render);
        _spaceportOverlay = new SpaceportOverlay(_ctx.Render, _ctx.Font, _assets);

        _universe.Settings = _ctx.Settings;
        _sprites.ImpactEffects.Connect(_universe.Events);
        _universe.Generate(_assets);
        _hudSubscription = _universe.Events.Subscribe<MessageRequested>(m => _hud.Post(m.Message, m.DurationSeconds));

        // Docking is a persist point: the player's position in the system is saved the moment they dock.
        _dockSaveSubscription = _universe.Events.Subscribe<PlayerDocked>(_ => _ctx.Saves.Save(_universe));
        _ctx.SetMouseVisible(false);
    }

    public void OnExit()
    {
        // Decided before the monitor goes away: a ship destroyed in the same frame the window closes is not saved either.
        _dockSaveSubscription?.Dispose();
        _dockSaveSubscription = null;
        _hudSubscription?.Dispose();
        _hudSubscription = null;
        bool playerDestroyed = _universe.Player.IsDestroyed;

        if (playerDestroyed)
        {
            // The player was destroyed: nothing from this session is saved. The menu offers the last saved state again.
            _ctx.Saves.ReloadInto(_ctx.Universes);
        }
        else
        {
            _ctx.Saves.Save(_universe);
        }
        _sprites?.ImpactEffects.Disconnect();
        _universe.Dispose();
        _galaxyMap?.Dispose();
        _spaceportOverlay?.Dispose();
        _spaceportOverlay = null;
        _assets?.Dispose();
        _galaxyMap = null;
        _assets = null;
    }

    public void Update(GameTime gameTime)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        // Destroyed: back to the menu without saving (OnExit sees the flag).
        if (_universe.Player.IsDestroyed)
        {
            _ctx.ShowMenu();
            return;
        }

        // Docked: the spaceport menu takes all the input, and nothing in space is simulated.
        if (_universe.IsDocked)
        {
            UpdateDocked(dt);
            return;
        }

        // Galaxy map intercepts all input while open.
        if (_galaxyMap.IsOpen)
        {
            if (_galaxyMap.Update(_universe, _ctx.ScreenWidth, _ctx.ScreenHeight, dt))
            {
                _universe.TargetJumpRoute(); // a route chosen on the map replaces the spaceport that was targeted for landing
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
            _universe.TargetJumpRoute(); // opening the map with a route selected targets that route
            _ctx.SetMouseVisible(true);
            return;
        }

        _hud.Update(dt);
        // Age existing hit visuals before this frame's impacts, so new hits draw at full life.
        _sprites.ImpactEffects.Update(dt);

        _universe.Update(dt, input);
        if (_universe.IsDocked)
            return; // the player docked this frame: there is no active star system to follow or draw now

        _camera.Update(_universe.Player.CameraTarget, dt, input);

        // While near a planet the player could dock at, say how.
        string dockingHint = _universe.DockingHint();
        if (dockingHint != null)
            _hud.Post(dockingHint, 1);

        _projectileRenderer.UpdateParticles(_universe.ActiveStarSystem.Projectiles, dt);
    }

    private void UpdateDocked(float dt)
    {
        EnsureSpaceportMenu();

        var input = _input.GetState();
        if (input.Exit)
        {
            if (!_spaceportMenu.Back()) // Escape closes an open store first, and undocks from the services
            {
                _universe.Undock();
                _spaceportMenu = null;
                _input.Rebaseline();
            }
            return;
        }

        _hud.Update(dt);
        _universe.Update(dt, input); // uploads finished nebulae; nothing else happens while docked
        _spaceportMenu.Update(input);

        if (!_universe.IsDocked)
        {
            _spaceportMenu = null;
            _input.Rebaseline();
        }
    }

    /// <summary>The menu for the planet the player is docked at (a new one when they dock somewhere new).</summary>
    private void EnsureSpaceportMenu()
    {
        if (_spaceportMenu == null || !ReferenceEquals(_spaceportMenu.Planet, _universe.DockedAt))
            _spaceportMenu = new SpaceportMenu(_universe.DockedAt, _universe);
    }

    public void Draw(GameTime gameTime)
    {
        if (_universe.IsDocked)
        {
            EnsureSpaceportMenu();
            _ctx.GraphicsDevice.Clear(ScreenContext.Background);
            _spaceportOverlay.Draw(_spaceportMenu, _ctx.ScreenWidth, _ctx.ScreenHeight);
            _ctx.Render.Begin(BatchMode.ScreenAlpha);
            _sprites.DrawMessage(_hud, _ctx.ScreenWidth, _ctx.ScreenHeight);
            return;
        }

        _ctx.GraphicsDevice.Clear(ScreenContext.Background);
        DrawWorld(_universe.ActiveStarSystem);
        DrawOverlay();

        if (_galaxyMap.IsOpen)
            _galaxyMap.Draw(_universe, _ctx.ScreenWidth, _ctx.ScreenHeight);
    }

    /// <summary>Draws the star system in fixed layer order. All batches go through <see cref="RenderService.Begin"/>, so the render service stays the single batch owner.</summary>
    private void DrawWorld(StarSystem sys)
    {
        int w = _camera.ScreenWidth, h = _camera.ScreenHeight;
        _sprites.DrawBackgroundStars(sys.BackgroundStars, w, h, _camera.Position, _camera.Zoom, layer: 0);
        _sprites.DrawNebula(sys.NebulaId, w, h, _camera.Position, _camera.Zoom, _ctx.Settings.ShowDebug);
        _sprites.DrawBackgroundStars(sys.BackgroundStars, w, h, _camera.Position, _camera.Zoom, layer: 1);

        foreach (var star in sys.Stars) _sprites.DrawStar(star);
        foreach (var planet in sys.Planets) _sprites.DrawPlanet(planet);
        foreach (var asteroid in sys.Asteroids) _sprites.DrawAsteroid(asteroid);

        var player = sys.ActivePlayer;
        _sprites.DrawPlayer(player);
        foreach (var npc in sys.Npcs) _sprites.DrawNPC(npc);

        _projectileRenderer.SetCameraMatrix(_camera.GetTransformMatrix());
        _projectileRenderer.DrawAll(sys.Projectiles);

        // The player's targets get brackets around them: a targeted ship, or the planet with a spaceport targeted for docking.
        if (player.Target != null && player.IsTargetValid(player.Target))
            _sprites.DrawTargetIndicator(player.Target);
        if (player.DockTarget != null)
            _sprites.DrawTargetIndicator(player.DockTarget);

        _debug.Draw(sys, player);
    }

    private void DrawOverlay()
    {
        int w = _ctx.ScreenWidth, h = _ctx.ScreenHeight;
        _ctx.Render.Begin(BatchMode.ScreenAlpha);
        _sprites.DrawSpeedBar(_universe.Player, w, h, _universe.Player.MaxSpeed);
        _sprites.DrawHud(_universe, w, h);

        _sprites.DrawMessage(_hud, w, h);
    }
}
