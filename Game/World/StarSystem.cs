using System.Collections.Generic;
using StrangeUniverse.Game.Entities;
using StrangeUniverse.Game.Systems;
using StrangeUniverse.Input;

namespace StrangeUniverse.Game.World;

/// <summary>Owns all game entities and drives the frame update.</summary>
public class StarSystem
{
    // ── Identity / seed ──────────────────────────────────────────────────────
    /// <summary>Deterministic seed derived from the parent Universe seed.</summary>
    public string Seed { get; set; } = string.Empty;

    // ── Generation configuration ─────────────────────────────────────────────
    public int    PlanetCount             { get; set; } = 7;
    public int    AsteroidCount           { get; set; } = 80;
    public float  SystemRadius            { get; set; } = 18000f;
    public float  AsteroidBeltInnerRadius { get; set; } = 4500f;
    public float  AsteroidBeltOuterRadius { get; set; } = 7000f;
    public int    BackgroundStarCount     { get; set; } = 600;
    public float  StarRadius              { get; set; } = 180f;
    public float  MinPlanetRadius         { get; set; } = 40f;
    public float  MaxPlanetRadius         { get; set; } = 130f;
    public float  MinAsteroidRadius       { get; set; } = 8f;
    public float  MaxAsteroidRadius       { get; set; } = 32f;

    // ── Entities ─────────────────────────────────────────────────────────────
    public Star                   Star            { get; set; } = new();
    public PlayerShip             Player          { get; set; } = null!;
    public List<Planet>           Planets         { get; }      = new();
    public List<Asteroid>         Asteroids       { get; }      = new();
    public List<BackgroundStar>   BackgroundStars { get; }      = new();
    public string                 NebulaTextureId { get; set; } = string.Empty;
    public float                  NebulaWorldSize { get; set; } = 40000f;

    private readonly PhysicsSystem   _physics   = new();
    private readonly CollisionSystem _collision = new();

    public void Update(float deltaTime, InputState input)
    {
        Player.Update(deltaTime, input);
        _physics.Update(Asteroids, deltaTime);
        _collision.Resolve(Player, Planets, Asteroids);
    }
}
