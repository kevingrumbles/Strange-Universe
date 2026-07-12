using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Strange_Universe.Game.World;
using StrangeUniverse.Game.Entities;
using StrangeUniverse.Game.Systems;
using StrangeUniverse.Input;
using StrangeUniverse.Rendering.ProceduralGeneration;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Xml.Linq;

namespace StrangeUniverse.Game.World;

/// <summary>Owns all game entities and drives the frame update.</summary>
public class StarSystem
{
    // ── Identity / seed ──────────────────────────────────────────────────────
    /// <summary>Deterministic seed derived from the parent Universe seed.</summary>
    public string Seed { get; set; } = string.Empty;
    public Guid SystemId { get; set; } = Guid.NewGuid();
    public HashSet<Guid> SystemConnectionIds { get; set; } = new();

    // ── Generation configuration ─────────────────────────────────────────────
    public int    PlanetCount             { get; set; }
    public int    AsteroidCount           { get; set; }
    public int    StarCount               { get; set; }
    public float  SystemRadius            { get; set; }
    public float  StarOrbitRadius         { get; set; }
    public float  StarOrbitSpeed          { get; set; }
    public float  AsteroidBeltInnerRadius { get; set; }
    public float  AsteroidBeltOuterRadius { get; set; }
    public int    BackgroundStarCount     { get; set; } 
    public float  StarRadius              { get; set; } 
    public float  MinPlanetRadius         { get; set; }
    public float  MaxPlanetRadius         { get; set; } 
    public float  MinAsteroidRadius       { get; set; } 
    public float  MaxAsteroidRadius       { get; set; } 
    public int SystemConnections { get; set; } = 1;

    // ── Entities ─────────────────────────────────────────────────────────────
    [JsonIgnore] public List<Star>                 Stars            { get; set; } = new();
    [JsonIgnore] public List<Planet>         Planets         { get; }      = new();
    [JsonIgnore] public List<Asteroid>       Asteroids       { get; }      = new();
    [JsonIgnore] public List<BackgroundStar> BackgroundStars { get; }      = new();
    [JsonIgnore] public string               NebulaTextureId { get; set; } = string.Empty;
    [JsonIgnore] public Random SystemRng { get; set; } = new Random();

    private readonly PhysicsSystem   _physics   = new();
    private readonly CollisionSystem _collision = new();

    public StarSystem()
    {
        if (string.IsNullOrEmpty(Seed)) Seed = SystemId.ToString();
        SystemRng = new Random(StaticHelpers.SeedHash(Seed));
        PlanetCount = (int)SystemRng.NextWeightedFloat(0, 7);
        AsteroidCount = (int)SystemRng.NextWeightedFloat(0, 120);
        StarCount = (int)SystemRng.NextWeightedFloat(1, 3);
        StarRadius = SystemRng.NextWeightedFloat(250f, 500f);
        switch (StarCount)
        {
            case 1: 
                StarOrbitRadius = 0f;  
                StarOrbitSpeed = 0f;
                break;
            case 2: 
                StarOrbitRadius = SystemRng.NextWeightedFloat(2000f, 4000f);
                StarOrbitSpeed  = SystemRng.NextWeightedFloat(0.0001f, 0.0003f);
                break;
            case 3: 
                StarOrbitRadius = SystemRng.NextWeightedFloat(3000f, 7000f);
                StarOrbitSpeed  = SystemRng.NextWeightedFloat(0.00005f, 0.0002f);
                break;
        }
        // The star core is the region occupied by all orbiting stars.
        // For a single star StarOrbitRadius == 0, so starCoreRadius == StarRadius.
        float starCoreRadius = StarOrbitRadius + StarRadius;
        // SystemRadius is always large enough to contain the star core plus a meaningful planetary region.
        float starCoreFootprint = StarOrbitRadius * 2f;
        SystemRadius = SystemRng.NextWeightedFloat(12000f + starCoreFootprint, 25000f + starCoreFootprint);
        BackgroundStarCount = (int)SystemRng.NextWeightedFloat(400, 800);

        AsteroidBeltInnerRadius = Math.Max(
            SystemRng.NextWeightedFloat(SystemRadius * 0.2f, SystemRadius * 0.4f),
            starCoreRadius * 2.5f);
        AsteroidBeltOuterRadius = SystemRng.NextWeightedFloat(AsteroidBeltInnerRadius * 1.2f, SystemRadius * 0.6f);
        
        MinPlanetRadius = SystemRng.NextWeightedFloat(90f, 120f);
        MaxPlanetRadius = SystemRng.NextWeightedFloat(150, 300f);
        MinAsteroidRadius = SystemRng.NextWeightedFloat(15f, 30f);
        MaxAsteroidRadius = SystemRng.NextWeightedFloat(40f, 50f);
        SystemConnections = (int)SystemRng.NextWeightedFloat(1, 4);
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

    // ── Generation ────────────────────────────────────────────────────────────

    public void Generate()
    {
        GenerateStars();
        GeneratePlanets();
        GenerateAsteroids();
        GenerateBackgroundStars();
        GenerateNebula();
    }

    public void AddSystemConnection(Guid otherSystemId)
    {
        SystemConnectionIds.Add(otherSystemId);
    }

    // ── Star ───────────────────────────────────────────────────────────────

    private void GenerateStars()
    {
        for (int i = 0; i < StarCount; i++)
        {
            Color starColor = StaticHelpers.StarColors[SystemRng.Next(StaticHelpers.StarColors.Length)];
            string Name = StaticHelpers.StarNames[SystemRng.Next(StaticHelpers.StarNames.Length)];
            while (Stars.Contains(Stars.Find(s => s.Name == Name)))
            {
                Name = StaticHelpers.StarNames[SystemRng.Next(StaticHelpers.StarNames.Length)];
            }

            string id = $"{SystemId.ToString()}_{Name}";
            var tex = StarTextureGenerator.Generate(starColor, StaticHelpers.SeedHash(Seed));
            Launcher.TextureCache.Register(id, tex);

            float initialAngle = MathHelper.TwoPi * i / StarCount;
            Star newStar = new Star
            {
                TextureId   = id,
                Radius      = StarRadius,
                Name        = Name,
                MinimapColor = starColor,
                OrbitRadius  = StarOrbitRadius,
                OrbitAngle   = initialAngle,
                OrbitSpeed   = StarOrbitSpeed,
            };
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
        var types = (PlanetType[])Enum.GetValues(typeof(PlanetType));

        float minOrbit  = Math.Max(StarRadius * 3.5f, StarOrbitRadius * 2f);
        float maxOrbit  = SystemRadius * 0.75f;

        int innerCount = Math.Max(1, PlanetCount / 2);
        int outerCount = PlanetCount - innerCount;

        PlacePlanets(types, innerCount, minOrbit, AsteroidBeltInnerRadius * 0.9f);
        PlacePlanets(types, outerCount, AsteroidBeltOuterRadius * 1.1f, maxOrbit);
    }

    private void PlacePlanets(PlanetType[] types, int count,
                              float minOrbit, float maxOrbit)
    {
        for (int i = 0; i < count; i++)
        {
            Planet newPlanet = new Planet(
                name: StaticHelpers.PlanetNames[Planets.Count % StaticHelpers.PlanetNames.Length],
                systemId: SystemId,
                minPlanetRadius: MinPlanetRadius,
                maxPlanetRadius: MaxPlanetRadius,
                systemRng: SystemRng,
                type: types[SystemRng.Next(types.Length)],
                minOrbit: minOrbit,
                maxOrbit: maxOrbit,
                planetNumber: i,
                totalPlanets: count
            );
            Planets.Add(newPlanet);
        }
    }

    // ── Asteroids ───────────────────────────────────────────────────────────

    private void GenerateAsteroids()
    {
        // Pre-generate a small palette of asteroid textures and reuse them
        const int PaletteSize = 15;
        var paletteIds = new string[PaletteSize];
        for (int i = 0; i < PaletteSize; i++)
        {
            paletteIds[i] = $"asteroid_tex_{i}";
            if (Launcher.TextureCache.TryGet(paletteIds[i], out Texture2D texture)) continue;
            var tex = AsteroidTextureGenerator.Generate(Launcher.GD, SystemRng.Next());
            Launcher.TextureCache.Register(paletteIds[i], tex);
        }

        for (int i = 0; i < AsteroidCount; i++)
        {
            float orbit = MathHelper.Lerp(AsteroidBeltInnerRadius, AsteroidBeltOuterRadius, (float)SystemRng.NextDouble());
            float angle = (float)(SystemRng.NextDouble() * MathHelper.TwoPi);

            float radius = MathHelper.Lerp(MinAsteroidRadius, MaxAsteroidRadius,
                                           (float)SystemRng.NextDouble());

            var asteroid = new Asteroid
            {
                TextureId = paletteIds[SystemRng.Next(PaletteSize)],
                Radius    = radius,
            };

            asteroid.Transform.Position = new Vector2(
                (float)Math.Cos(angle) * orbit,
                (float)Math.Sin(angle) * orbit);
            asteroid.Transform.Rotation = (float)(SystemRng.NextDouble() * MathHelper.TwoPi);

            float speed     = MathHelper.Lerp(8f, 30f, (float)SystemRng.NextDouble());
            float perpAngle = angle + MathHelper.PiOver2;
            asteroid.Physics.Velocity = new Vector2(
                (float)Math.Cos(perpAngle) * speed,
                (float)Math.Sin(perpAngle) * speed);
            asteroid.Physics.AngularVelocity = MathHelper.Lerp(-0.4f, 0.4f, (float)SystemRng.NextDouble());

            Asteroids.Add(asteroid);
        }
    }

    // ── Background stars ──────────────────────────────────────────────────

    private void GenerateBackgroundStars()
    {
        // Positions are in a virtual 2048×2048 space used only for parallax scrolling
        const float VirtualSize = 2048f;
        for (int i = 0; i < BackgroundStarCount; i++)
        {
            BackgroundStars.Add(new BackgroundStar
            {
                Position   = new Vector2(
                    (float)SystemRng.NextDouble() * VirtualSize,
                    (float)SystemRng.NextDouble() * VirtualSize),
                Brightness = MathHelper.Lerp(0.35f, 1f, (float)SystemRng.NextDouble()),
                Size       = SystemRng.NextDouble() < 0.15 ? 2f : 1f,
                // Randomly placed in front of (layer 1) or behind (layer 0) the nebula
                Layer      = SystemRng.NextDouble() < 0.5 ? 1 : 0,
            });
        }
    }

    // ── Nebula ───────────────────────────────────────────────────────────────

    private void GenerateNebula()
    {
        NebulaTextureId = $"{SystemId.ToString()}_nebula";
        var tex = NebulaGenerator.Generate(StaticHelpers.SeedHash(Seed) + 77777);
        Launcher.TextureCache.Register(NebulaTextureId, tex);
    }
}
