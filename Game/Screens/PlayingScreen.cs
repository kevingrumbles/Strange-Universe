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
    private WorldRenderer _world;
    private HudRenderer _hudRenderer;
    private GalaxyMapOverlay _galaxyMap;
    private PlayerDeathMonitor _deathMonitor;
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
        _world = new WorldRenderer(_sprites, _projectileRenderer,
            new DebugRenderer(_ctx.Render, _camera, _ctx.Settings), _ctx.Settings);
        _hudRenderer = new HudRenderer(_ctx.Render, _ctx.Font);
        _galaxyMap = new GalaxyMapOverlay(_ctx.Font, _ctx.GraphicsDevice, _ctx.Render);
        _spaceportOverlay = new SpaceportOverlay(_ctx.Render, _ctx.Font, _assets);

        _universe.Settings = _ctx.Settings;
        _deathMonitor = new PlayerDeathMonitor(_universe.Events, _universe.Player);
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
        bool playerDestroyed = _deathMonitor?.PlayerDestroyed ?? false;
        _deathMonitor?.Dispose();
        _deathMonitor = null;

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
        if (_deathMonitor.PlayerDestroyed)
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
            if (!_spaceportMenu.Back()) // Escape closes an open store first, and leaves for the main menu from the services
                _ctx.ShowMenu();
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
            _hudRenderer.DrawMessage(_hud, _ctx.ScreenWidth, _ctx.ScreenHeight);
            return;
        }

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
