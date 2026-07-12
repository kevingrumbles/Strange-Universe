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
    public float  SystemRadius            { get; set; } 
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
    [JsonIgnore] public Star                 Star            { get; set; } = new();
    [JsonIgnore] public List<Planet>         Planets         { get; }      = new();
    [JsonIgnore] public List<Asteroid>       Asteroids       { get; }      = new();
    [JsonIgnore] public List<BackgroundStar> BackgroundStars { get; }      = new();
    [JsonIgnore] public string               NebulaTextureId { get; set; } = string.Empty;
    [JsonIgnore] public Random systemRng { get; set; } = new Random();

    private readonly PhysicsSystem   _physics   = new();
    private readonly CollisionSystem _collision = new();

    public StarSystem()
    {
        if (string.IsNullOrEmpty(Seed)) Seed = SystemId.ToString();
        systemRng = new Random(StaticHelpers.SeedHash(Seed));
        PlanetCount = (int)systemRng.NextWeightedFloat(0, 7);
        AsteroidCount = (int)systemRng.NextWeightedFloat(0, 120);
        SystemRadius = systemRng.NextWeightedFloat(12000f, 25000f);
        AsteroidBeltInnerRadius = systemRng.NextWeightedFloat(SystemRadius * 0.2f, SystemRadius * 0.4f);
        AsteroidBeltOuterRadius = systemRng.NextWeightedFloat(AsteroidBeltInnerRadius * 1.2f, SystemRadius * 0.6f);
        BackgroundStarCount = (int)systemRng.NextWeightedFloat(400, 800);
        StarRadius = systemRng.NextWeightedFloat(120f, 250f);
        MinPlanetRadius = systemRng.NextWeightedFloat(30f, 60f);
        MaxPlanetRadius = systemRng.NextWeightedFloat(80f, 150f);
        MinAsteroidRadius = systemRng.NextWeightedFloat(5f, 15f);
        MaxAsteroidRadius = systemRng.NextWeightedFloat(20f, 40f);
        SystemConnections = (int)systemRng.NextWeightedFloat(1, 4);
    }

    public void Update(Player player, float deltaTime, InputState input)
    {
        player.Update(deltaTime, input);
        _physics.Update(Asteroids, deltaTime);
        _collision.Resolve(player, Planets, Asteroids);
    }

    // ── Generation ────────────────────────────────────────────────────────────

    public void Generate()
    {
        GenerateStar();
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

    private void GenerateStar()
    {
        var starColors = new[]
        {
            new Color(255, 240, 180),   // warm yellow (G-type)
            new Color(255, 200, 120),   // orange (K-type)
            new Color(255, 160, 80),    // orange-red (M-type)
            new Color(180, 210, 255),   // blue-white (A-type)
        };
        Color starColor = starColors[systemRng.Next(starColors.Length)];

        const string id = "star";
        var tex = StarTextureGenerator.Generate(starColor, StaticHelpers.SeedHash(Seed));
        Launcher.TextureCache.Register(id, tex);

        Star = new Star
        {
            TextureId    = id,
            Radius       = StarRadius,
            Name         = "Sol",
            MinimapColor = starColor,
        };
        Star.Transform.Position = Vector2.Zero;
    }

    // ── Planets ────────────────────────────────────────────────────────────

    private void GeneratePlanets()
    {
        var types = (PlanetType[])Enum.GetValues(typeof(PlanetType));

        float minOrbit  = StarRadius * 3.5f;
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
                systemRng: systemRng,
                type: types[systemRng.Next(types.Length)],
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
            var tex = AsteroidTextureGenerator.Generate(Launcher.GD, systemRng.Next());
            Launcher.TextureCache.Register(paletteIds[i], tex);
        }

        for (int i = 0; i < AsteroidCount; i++)
        {
            float orbit = MathHelper.Lerp(AsteroidBeltInnerRadius, AsteroidBeltOuterRadius, (float)systemRng.NextDouble());
            float angle = (float)(systemRng.NextDouble() * MathHelper.TwoPi);

            float radius = MathHelper.Lerp(MinAsteroidRadius, MaxAsteroidRadius,
                                           (float)systemRng.NextDouble());

            var asteroid = new Asteroid
            {
                TextureId = paletteIds[systemRng.Next(PaletteSize)],
                Radius    = radius,
            };

            asteroid.Transform.Position = new Vector2(
                (float)Math.Cos(angle) * orbit,
                (float)Math.Sin(angle) * orbit);
            asteroid.Transform.Rotation = (float)(systemRng.NextDouble() * MathHelper.TwoPi);

            float speed     = MathHelper.Lerp(8f, 30f, (float)systemRng.NextDouble());
            float perpAngle = angle + MathHelper.PiOver2;
            asteroid.Physics.Velocity = new Vector2(
                (float)Math.Cos(perpAngle) * speed,
                (float)Math.Sin(perpAngle) * speed);
            asteroid.Physics.AngularVelocity = MathHelper.Lerp(-0.4f, 0.4f, (float)systemRng.NextDouble());

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
                    (float)systemRng.NextDouble() * VirtualSize,
                    (float)systemRng.NextDouble() * VirtualSize),
                Brightness = MathHelper.Lerp(0.35f, 1f, (float)systemRng.NextDouble()),
                Size       = systemRng.NextDouble() < 0.15 ? 2f : 1f,
                // Randomly placed in front of (layer 1) or behind (layer 0) the nebula
                Layer      = systemRng.NextDouble() < 0.5 ? 1 : 0,
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
