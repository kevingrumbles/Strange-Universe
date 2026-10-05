using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework;
using Strange_Universe.Game.Components;
using Strange_Universe.Game.EventSystem;
using Strange_Universe.Game.Systems;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Strange_Universe.Game.Entities;

/// <summary>Owns all game entities and drives the frame update.</summary>
public class StarSystem
{
    [JsonIgnore] public string SystemId { get { return Node.SystemId;  }  }
    [JsonIgnore] public HashSet<string> SystemConnectionIds { get { return Node.SystemConnectionIds; } }
    [JsonIgnore] public string Name { get { return Node.Name; } }
    [JsonIgnore] public Vector2 GalaxyPosition { get { return Node.GalaxyPosition; } }
    [JsonIgnore] public StarSystemNode Node { get; set; }
    [JsonIgnore] public Player ActivePlayer => Universe?.Player;
    [JsonIgnore] public Universe Universe { get; }
    /// <summary>Render-side art requests; null when running without graphics.</summary>
    [JsonIgnore] public IAssetRequests Assets { get; }
    [JsonIgnore] public string DisplayName { get { return Node.DisplayName; } }
    [JsonIgnore] public bool Discovered { get { return Node.Discovered; } }
    [JsonIgnore] public int    PlanetCount             { get; set; }
    [JsonIgnore] public int    AsteroidCount           { get; set; }
    [JsonIgnore] public int    StarCount               { get; set; }
    [JsonIgnore] public float  SystemRadius            { get; set; }
    [JsonIgnore] public float  MandevilleRadius        { get; set; }
    [JsonIgnore] public float  StarOrbitRadius         { get; set; }
    [JsonIgnore] public float  StarOrbitSpeed          { get; set; }
    [JsonIgnore] public float  AsteroidBeltInnerRadius { get; set; }
    [JsonIgnore] public float  AsteroidBeltOuterRadius { get; set; }
    [JsonIgnore] public int    BackgroundStarCount     { get; set; }
    [JsonIgnore] public float  StarRadius              { get; set; }
    [JsonIgnore] public float  MinPlanetRadius         { get; set; }
    [JsonIgnore] public float  MaxPlanetRadius         { get; set; }
    [JsonIgnore] public float  MinAsteroidRadius       { get; set; }
    [JsonIgnore] public float  MaxAsteroidRadius       { get; set; }
    [JsonIgnore] public int SystemConnectionCount { get; set; } = 1;
    [JsonIgnore] public int InnerPlanetCount { get; set; }
    [JsonIgnore] public List<Star>           Stars            { get; set; } = new();
    [JsonIgnore] public List<Planet>         Planets         { get; }      = new();
    [JsonIgnore] public List<Asteroid>       Asteroids       { get; }      = new();
    [JsonIgnore] public List<BackgroundStar> BackgroundStars { get; }      = new();
    [JsonIgnore] public List<Nonplayer> Npcs            { get; }      = new();
    [JsonIgnore] public List<Projectile> Projectiles     { get; }      = new();
    [JsonIgnore] public string        NebulaId        { get; internal set; }

    private readonly EventController _eventController;
    private readonly CollisionSystem _collision;
    private readonly ProjectileCollisionSystem _projectileCollision = new();

    public StarSystem() 
    {
        _collision = new CollisionSystem();

    }

    public StarSystem(StarSystemNode node, Universe universe, IAssetRequests assets, Random random = null)
    {
        Node = node;
        Universe = universe;
        Assets = assets;
        Node.Discovered = true;
        _collision = new CollisionSystem(universe?.Settings);

        _eventController = new EventController(this);
        Spatial = new SystemSpatialQueries(this, random);
        StarSystemGenerator.Populate(this);
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

        _projectileCollision.Resolve(Projectiles, ActivePlayer, Npcs, Asteroids, deltaTime);

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
    /// </summary>
    public event Action<Ship, Projectile, bool> ShipHit;

    internal void RaiseShipHit(Ship ship, Projectile projectile, bool shieldWasUp)
        => ShipHit?.Invoke(ship, projectile, shieldWasUp);

    /// <summary>Adds an NPC to this system and attaches the system to it.</summary>
    public void AddNpc(Nonplayer npc)
    {
        npc.StarSystem = this;
        Assets?.EnsureShipArt(npc.ShipType);
        Npcs.Add(npc);
    }
}
