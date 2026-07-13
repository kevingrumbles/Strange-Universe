using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Strange_Universe.Game.Components;
using StrangeUniverse;
using StrangeUniverse.Game.Entities;
using StrangeUniverse.Game.Systems;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Strange_Universe.Game.Entities;

/// <summary>Owns all game entities and drives the frame update.</summary>
public class StarSystem
{
    // ── Identity / seed ──────────────────────────────────────────────────────
    /// <summary>Deterministic seed derived from the parent Universe seed.</summary>
    public string SystemId { get; set; }
    public HashSet<string> SystemConnectionIds { get; set; } = new();
    public string Name { get; set; }

    // ── Generation configuration ─────────────────────────────────────────────
    [JsonIgnore] public int    PlanetCount             { get; set; }
    [JsonIgnore] public int    AsteroidCount           { get; set; }
    [JsonIgnore] public int    StarCount               { get; set; }
    [JsonIgnore] public float  SystemRadius            { get; set; }
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

    // ── Entities ─────────────────────────────────────────────────────────────
    [JsonIgnore] public List<Star>           Stars            { get; set; } = new();
    [JsonIgnore] public List<Planet>         Planets         { get; }      = new();
    [JsonIgnore] public List<Asteroid>       Asteroids       { get; }      = new();
    [JsonIgnore] public List<BackgroundStar> BackgroundStars { get; }      = new();
    [JsonIgnore] public Nebula               Nebula { get; set; }
    [JsonIgnore] public Universe             Universe { get; set; }
    private Random _systemRng { get; set; } = null;

    private readonly PhysicsSystem   _physics   = new();
    private readonly CollisionSystem _collision = new();

    public StarSystem() { }
    public StarSystem(Universe parentUniverse, StarSystem backConnection = null)
    {
        Name = parentUniverse.GetStarSystemName();
        Universe = parentUniverse;
        SystemId = $"{parentUniverse.Seed}_{Guid.NewGuid().ToString()}";

        if (backConnection != null)
        {
            SystemConnectionIds.Add(backConnection.SystemId);
        }
    }

    public void Generate(Universe parentUniverse)
    {
        Universe = parentUniverse;
        _systemRng = new Random(StaticHelpers.SeedHash(SystemId));
        PlanetCount = _systemRng.Next(0, 7);
        AsteroidCount = _systemRng.Next(0, 120);
        StarCount = _systemRng.Next(1, 3);
        StarRadius = _systemRng.NextWeightedFloat(250f, 500f);
        switch (StarCount)
        {
            case 1: 
                StarOrbitRadius = 0f;  
                StarOrbitSpeed = 0f;
                break;
            case 2: 
                StarOrbitRadius = _systemRng.NextWeightedFloat(2000f, 4000f);
                StarOrbitSpeed  = _systemRng.NextWeightedFloat(0.0001f, 0.0003f);
                break;
            case 3: 
                StarOrbitRadius = _systemRng.NextWeightedFloat(3000f, 7000f);
                StarOrbitSpeed  = _systemRng.NextWeightedFloat(0.00005f, 0.0002f);
                break;
        }
        // The star core is the region occupied by all orbiting stars.
        // For a single star StarOrbitRadius == 0, so starCoreRadius == StarRadius.
        float starCoreRadius = StarOrbitRadius + StarRadius;
        // SystemRadius is always large enough to contain the star core plus a meaningful planetary region.
        float starCoreFootprint = StarOrbitRadius * 2f;
        SystemRadius = _systemRng.NextWeightedFloat(12000f + starCoreFootprint, 25000f + starCoreFootprint);
        BackgroundStarCount = _systemRng.Next(400, 800);

        AsteroidBeltInnerRadius = Math.Max(
            _systemRng.NextWeightedFloat(SystemRadius * 0.2f, SystemRadius * 0.4f),
            starCoreRadius * 2.5f);
        AsteroidBeltOuterRadius = _systemRng.NextWeightedFloat(AsteroidBeltInnerRadius * 1.2f, SystemRadius * 0.6f);
        
        MinPlanetRadius = _systemRng.NextWeightedFloat(90f, 120f);
        MaxPlanetRadius = _systemRng.NextWeightedFloat(150, 300f);
        MinAsteroidRadius = _systemRng.NextWeightedFloat(15f, 30f);
        MaxAsteroidRadius = _systemRng.NextWeightedFloat(40f, 50f);
        SystemConnectionCount = _systemRng.Next(1, 4);
        InnerPlanetCount =_systemRng.Next(PlanetCount);
        GenerateStars();
        GeneratePlanets();
        GenerateAsteroids();
        GenerateBackgroundStars();
        GenerateNebula();
        GenerateConnections();
    }

    public void Update(Player player, float deltaTime, InputState input)
    {
        player.Update(deltaTime, input);
        UpdateStarOrbits(deltaTime);
        _physics.Update(Asteroids, deltaTime);
        _collision.Resolve(player, Planets, Asteroids);
    }

    private void UpdateStarOrbits(float deltaTime)
    {
        if (Stars.Count <= 1) return;
        foreach (var star in Stars)
        {
            star.OrbitAngle += star.OrbitSpeed * deltaTime;
            star.Transform.Position = new Vector2(
                MathF.Cos(star.OrbitAngle) * star.OrbitRadius,
                MathF.Sin(star.OrbitAngle) * star.OrbitRadius);
        }
    }

    public void AddSystemConnection(string otherSystemId)
    {
        SystemConnectionIds.Add(otherSystemId);
    }

    private void GenerateConnections()
    {
        for (int i = SystemConnectionIds.Count; i < SystemConnectionCount; i++)
        {
            StarSystem newConnection = new StarSystem(parentUniverse: Universe, backConnection: this);
            SystemConnectionIds.Add(newConnection.SystemId);
        }
    }

    private void GenerateStars()
    {
        for (int i = 0; i < StarCount; i++)
        {
            string starId = $"{SystemId}_Star_{i}";
            Random starRandom = new Random(StaticHelpers.SeedHash(starId));
            Color starColor = StaticHelpers.StarColors[starRandom.Next(StaticHelpers.StarColors.Length)];
            string name = null;
            while (name is null || Stars.Contains(Stars.Find(s => s.Name == name)))
            {
                name = StaticHelpers.StarNames[starRandom.Next(StaticHelpers.StarNames.Length)];
            }
            
            float initialAngle = MathHelper.TwoPi * i / StarCount;
            Star newStar = new Star(starId, StarRadius, name, starColor, StarOrbitRadius, initialAngle, StarOrbitSpeed);
            newStar.Transform.Position = StarCount == 1
                ? Vector2.Zero
                : new Vector2(
                    MathF.Cos(initialAngle) * StarOrbitRadius,
                    MathF.Sin(initialAngle) * StarOrbitRadius);
            Stars.Add(newStar);
        }
    }

    // ── Planets ────────────────────────────────────────────────────────────

    private void GeneratePlanets()
    {
        for (int i = 1; i <= PlanetCount; i++)
        {
            switch (i) 
            {
                case var _ when i <= InnerPlanetCount:
                    Planets.Add(new Planet(planetId: $"{SystemId}_Planet_{i}",
                                            minPlanetRadius: MinPlanetRadius,
                                            maxPlanetRadius: MaxPlanetRadius,
                                            minOrbit: Math.Max(StarRadius * 3.5f, StarOrbitRadius * 2f),
                                            maxOrbit: AsteroidBeltInnerRadius * 0.9f,
                                            planetNumber: i,
                                            totalPlanets: PlanetCount));
                    break;
                default:
                    Planets.Add(new Planet(planetId: $"{SystemId}_Planet_{i}",
                                          minPlanetRadius: MinPlanetRadius,
                                          maxPlanetRadius: MaxPlanetRadius,
                                          minOrbit: AsteroidBeltOuterRadius * 1.1f,
                                          maxOrbit: SystemRadius * 0.75f,
                                          planetNumber: i,
                                          totalPlanets: PlanetCount));
                    break;
            }
        }
    }

    // ── Asteroids ───────────────────────────────────────────────────────────

    private void GenerateAsteroids()
    {
        // Pre-generate a small palette of asteroid textures and reuse them
        Random asteroidsRng = new Random(StaticHelpers.SeedHash($"{SystemId}_Asteroids"));
        const int PaletteSize = 15;
        var paletteIds = new string[PaletteSize];
        for (int i = 0; i < PaletteSize; i++)
        {
            paletteIds[i] = $"asteroid_tex_{i}";
            if (Launcher.TextureCache.TryGet(paletteIds[i], out Texture2D texture)) continue;

            var tex = Asteroid.Generate(Launcher.GD, asteroidsRng.Next());
            Launcher.TextureCache.Register(paletteIds[i], tex);
        }

        for (int i = 0; i < AsteroidCount; i++)
        {
            float orbit = MathHelper.Lerp(AsteroidBeltInnerRadius, AsteroidBeltOuterRadius, (float)asteroidsRng.NextDouble());
            float angle = (float)(asteroidsRng.NextDouble() * MathHelper.TwoPi);
            float radius = MathHelper.Lerp(MinAsteroidRadius, MaxAsteroidRadius, (float)asteroidsRng.NextDouble());
            string textureId = paletteIds[asteroidsRng.Next(PaletteSize)];
            Asteroids.Add(new Asteroid(angle, orbit, radius, textureId, asteroidsRng));
        }
    }

    // ── Background stars ──────────────────────────────────────────────────

    private void GenerateBackgroundStars()
    {
        Random backgroundStarsRng = new Random(StaticHelpers.SeedHash($"{SystemId}_BackgroundStars"));

        // Positions are in a virtual 2048×2048 space used only for parallax scrolling
        const float VirtualSize = 2048f;
        for (int i = 0; i < BackgroundStarCount; i++)
        {
            BackgroundStars.Add(new BackgroundStar
            {
                Position   = new Vector2(
                    (float)backgroundStarsRng.NextDouble() * VirtualSize,
                    (float)backgroundStarsRng.NextDouble() * VirtualSize),
                Brightness = MathHelper.Lerp(0.35f, 1f, (float)backgroundStarsRng.NextDouble()),
                Size       = backgroundStarsRng.NextDouble() < 0.15 ? 2f : 1f,
                // Randomly placed in front of (layer 1) or behind (layer 0) the nebula
                Layer      = backgroundStarsRng.NextDouble() < 0.5 ? 1 : 0,
            });
        }
    }

    // ── Nebula ───────────────────────────────────────────────────────────────

    private void GenerateNebula()
    {
        Nebula = new Nebula($"{SystemId}_nebula");
        Launcher.TextureCache.Register(Nebula.Id, Nebula.Texture);
    }
}
