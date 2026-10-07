using Microsoft.Xna.Framework;
using Strange_Universe.Game.Components;
using Strange_Universe.Game.EventSystem;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

using Strange_Universe.Game.Simulation;
namespace Strange_Universe.Game.Entities;

/// <summary>Owns all game entities and drives the frame update.</summary>
public class StarSystem
{
    [JsonIgnore] public string SystemId { get { return Node.SystemId;  }  }
    [JsonIgnore] public HashSet<string> SystemConnectionIds { get { return Node.SystemConnectionIds; } }
    [JsonIgnore] public string Name { get { return Node.Name; } }
    [JsonIgnore] public Vector2 GalaxyPosition { get { return Node.GalaxyPosition; } }
    [JsonIgnore] public StarSystemNode Node { get; }
    [JsonIgnore] public Player ActivePlayer => Universe?.Player;
    [JsonIgnore] public Universe Universe { get; }
    /// <summary>Render-side art requests. Never null (<see cref="NullAssetRequests"/> when headless).</summary>
    [JsonIgnore] public IAssetRequests Assets { get; }
    [JsonIgnore] public string DisplayName { get { return Node.DisplayName; } }
    [JsonIgnore] public bool Discovered { get { return Node.Discovered; } }
    [JsonIgnore] public int    PlanetCount             => Layout.PlanetCount;
    [JsonIgnore] public int    AsteroidCount           => Layout.AsteroidCount;
    [JsonIgnore] public int    StarCount               => Layout.StarCount;
    [JsonIgnore] public float  SystemRadius            => Layout.SystemRadius;
    [JsonIgnore] public float  MandevilleRadius        => Layout.MandevilleRadius;
    [JsonIgnore] public float  StarOrbitRadius         => Layout.StarOrbitRadius;
    [JsonIgnore] public float  StarOrbitSpeed          => Layout.StarOrbitSpeed;
    [JsonIgnore] public float  AsteroidBeltInnerRadius => Layout.AsteroidBeltInnerRadius;
    [JsonIgnore] public float  AsteroidBeltOuterRadius => Layout.AsteroidBeltOuterRadius;
    [JsonIgnore] public int    BackgroundStarCount     => Layout.BackgroundStarCount;
    [JsonIgnore] public float  StarRadius              => Layout.StarRadius;
    [JsonIgnore] public float  MinPlanetRadius         => Layout.MinPlanetRadius;
    [JsonIgnore] public float  MaxPlanetRadius         => Layout.MaxPlanetRadius;
    [JsonIgnore] public float  MinAsteroidRadius       => Layout.MinAsteroidRadius;
    [JsonIgnore] public float  MaxAsteroidRadius       => Layout.MaxAsteroidRadius;
    [JsonIgnore] public int SystemConnectionCount => Layout.SystemConnectionCount;
    [JsonIgnore] public int InnerPlanetCount => Layout.InnerPlanetCount;
    [JsonIgnore] public List<Star>           Stars           { get; }
    [JsonIgnore] public List<Planet>         Planets         { get; }
    [JsonIgnore] public List<Asteroid>       Asteroids       { get; }
    [JsonIgnore] public List<BackgroundStar> BackgroundStars { get; }
    [JsonIgnore] public List<Nonplayer> Npcs            { get; }      = new();
    [JsonIgnore] public List<Projectile> Projectiles     { get; }      = new();
    [JsonIgnore] public string        NebulaId        { get; }

    /// <summary>The generated parameters this system was built from.</summary>
    [JsonIgnore] public StarSystemLayout Layout { get; }

    private readonly EventController _eventController;
    private readonly CollisionSystem _collision;
    private readonly ProjectileCollisionSystem _projectileCollision = new();

    /// <summary>
    /// Builds a system from an already generated <paramref name="layout"/>. Construction has no side
    /// effects on the node, the universe or the asset service; see <see cref="Universe.EnterSystem"/>.
    /// </summary>
    public StarSystem(StarSystemNode node, Universe universe, StarSystemLayout layout, IAssetRequests assets, Random random = null)
    {
        Node = node;
        Universe = universe;
        Layout = layout;
        Assets = assets ?? NullAssetRequests.Instance;
        _collision = new CollisionSystem(universe?.Settings);

        Stars = new List<Star>(layout.Stars);
        Planets = new List<Planet>(layout.Planets);
        Asteroids = new List<Asteroid>(layout.Asteroids);
        BackgroundStars = new List<BackgroundStar>(layout.BackgroundStars);
        NebulaId = Universe.NebulaPoolId(universe.Seed, layout.NebulaIndex);

        _eventController = new EventController(this);
        Spatial = new SystemSpatialQueries(this, random);
    }

    /// <summary>Generates the layout for <paramref name="node"/> and builds its system. Does not request assets or expand the galaxy.</summary>
    internal static StarSystem Create(StarSystemNode node, Universe universe, IAssetRequests assets, Random random = null)
        => new(node, universe, StarSystemGenerator.Generate(node, Universe.NebulaPoolSize), assets, random);

    /// <summary>Requests the textures this system's stars, planets and asteroids need from the asset service.</summary>
    internal void AttachAssets()
    {
        foreach (var star in Stars) Assets.EnsureStarTexture(star);
        foreach (var planet in Planets) Assets.EnsurePlanetTexture(planet);

        // Palette art is a universe-wide resource, so its seed comes from the universe seed,
        // not from whichever system happens to request it first.
        for (int i = 0; i < StarSystemGenerator.AsteroidPaletteSize; i++)
            Assets.EnsureAsteroidTexture(StarSystemGenerator.AsteroidPaletteId(i),
                ProceduralHelpers.SeedHash($"{Universe.Seed}_asteroid_tex_{i}"));
    }

    /// <summary>Gravity, safe-location and entry-point queries for this system.</summary>
    [JsonIgnore] public SystemSpatialQueries Spatial { get; }

    public void Update(float deltaTime, InputState input)
    {
        ActivePlayer.Update(deltaTime, input);

        // Update NPCs using AI pipeline (Behavior -> Ship autopilot -> Ship physics)
        foreach (var npc in Npcs)
        {
            npc.Update(deltaTime);
        }

        // Remove NPCs that have left the system (e.g., merchants after jumping)
        Npcs.RemoveAll(npc => npc.Remove);

        _eventController.Update(deltaTime);

        UpdateStarOrbits(deltaTime);
        foreach (var asteroid in Asteroids)
            asteroid.Update(deltaTime);

        // Player collisions with static objects
        _collision.Resolve(ActivePlayer, Planets, Asteroids);

        // NPC collisions with static objects
        foreach (var npc in Npcs)
        {
            _collision.ResolveNPC(npc, Planets, Asteroids);
        }

        // Ship-to-ship collisions (player vs NPCs and NPC vs NPC)
        _collision.ResolveShipToShip(ActivePlayer, Npcs);

        // Move projectiles, then test for impacts before despawning.
        foreach (var projectile in Projectiles)
            projectile.Update(deltaTime);

        _projectileCollision.Resolve(Projectiles, ActivePlayer, Npcs, Asteroids, deltaTime, Universe?.Events);

        // Remove projectiles that expired or struck something
        Projectiles.RemoveAll(p => p.IsExpired);

        // Sweep up anything destroyed by this frame's impacts.
        // Asteroids linger until their debris has finished playing.
        Asteroids.RemoveAll(a => a.IsGone);
        Npcs.RemoveAll(npc => npc.Remove || npc.IsDestroyed);
    }

    private void UpdateStarOrbits(float deltaTime)
    {
        if (Stars.Count <= 1) return;
        foreach (var star in Stars)
        {
            star.OrbitAngle += star.OrbitSpeed * deltaTime;
            star.Position = new Vector2(
                MathF.Cos(star.OrbitAngle) * star.OrbitRadius,
                MathF.Sin(star.OrbitAngle) * star.OrbitRadius);

            // Update gravity well center as star moves
            star.GravityWell.Center = star.Position;
        }
    }

    /// <summary>
    /// Raised when a projectile damages a ship. Arguments: ship hit, projectile, whether shields were up.
    /// Presentation code subscribes to show hit effects; the simulation keeps no visual state.
    /// Compatibility shim: new code should subscribe to <see cref="ShipDamaged"/> on <see cref="Universe.Events"/>.
    /// </summary>
    public event Action<Ship, Projectile, bool> ShipHit;

    internal void RaiseShipHit(Ship ship, Projectile projectile, bool shieldWasUp)
    {
        Universe?.Events.Publish(new ShipDamaged(ship, projectile, shieldWasUp));
        ShipHit?.Invoke(ship, projectile, shieldWasUp);
    }

    /// <summary>Adds an NPC to this system and attaches the system to it.</summary>
    public void AddNpc(Nonplayer npc)
    {
        npc.StarSystem = this;
        Assets.EnsureShipArt(npc.ShipType);
        Npcs.Add(npc);
    }
}
