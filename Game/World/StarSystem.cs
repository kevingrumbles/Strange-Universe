using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Text.Json.Serialization;
using Strange_Universe.Game.World;
using StrangeUniverse.Game.Entities;
using StrangeUniverse.Game.Systems;
using StrangeUniverse.Input;
using StrangeUniverse.Rendering.ProceduralGeneration;
using StrangeUniverse.Rendering.SpriteRenderer;

namespace StrangeUniverse.Game.World;

/// <summary>Owns all game entities and drives the frame update.</summary>
public class StarSystem
{
    // ── Identity / seed ──────────────────────────────────────────────────────
    /// <summary>Deterministic seed derived from the parent Universe seed.</summary>
    public string Seed { get; set; } = string.Empty;
    public Guid SystemId { get; set; } = Guid.NewGuid();

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
    [JsonIgnore] public Star                 Star            { get; set; } = new();
    [JsonIgnore] public List<Planet>         Planets         { get; }      = new();
    [JsonIgnore] public List<Asteroid>       Asteroids       { get; }      = new();
    [JsonIgnore] public List<BackgroundStar> BackgroundStars { get; }      = new();
    [JsonIgnore] public string               NebulaTextureId { get; set; } = string.Empty;
    [JsonIgnore] public float                NebulaWorldSize { get; set; } = 40000f;

    private readonly PhysicsSystem   _physics   = new();
    private readonly CollisionSystem _collision = new();

    public void Update(Player player, float deltaTime, InputState input)
    {
        player.Update(deltaTime, input);
        _physics.Update(Asteroids, deltaTime);
        _collision.Resolve(player, Planets, Asteroids);
    }

    // ── Generation ────────────────────────────────────────────────────────────

    private static readonly string[] PlanetNames =
    {
        "Aether", "Boras", "Calyss", "Drevon", "Eston",
        "Fyrath", "Gavorn", "Helix", "Iridia", "Joras"
    };

    public void Generate(int universeSeedHash)
    {
        var rng    = new Random(SeedHash(Seed));
        GenerateStar(rng, universeSeedHash);
        GeneratePlanets(rng);
        GenerateAsteroids(rng);
        GenerateBackgroundStars(rng);
        GenerateNebula(rng, universeSeedHash);
    }

    private static int SeedHash(string s)
    {
        unchecked
        {
            uint hash = 2166136261u;
            foreach (char c in s)
                hash = (hash ^ c) * 16777619u;
            return (int)hash;
        }
    }

    // ── Star ───────────────────────────────────────────────────────────────

    private void GenerateStar(Random rng, int universeSeedHash)
    {
        var starColors = new[]
        {
            new Color(255, 240, 180),   // warm yellow (G-type)
            new Color(255, 200, 120),   // orange (K-type)
            new Color(255, 160, 80),    // orange-red (M-type)
            new Color(180, 210, 255),   // blue-white (A-type)
        };
        Color starColor = starColors[rng.Next(starColors.Length)];

        const string id = "star";
        var tex = StarTextureGenerator.Generate(starColor, universeSeedHash);
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

    private void GeneratePlanets(Random rng)
    {
        var types = (PlanetType[])Enum.GetValues(typeof(PlanetType));

        float minOrbit  = StarRadius * 3.5f;
        float beltInner = AsteroidBeltInnerRadius;
        float beltOuter = AsteroidBeltOuterRadius;
        float maxOrbit  = SystemRadius * 0.75f;

        int innerCount = Math.Max(1, PlanetCount / 2);
        int outerCount = PlanetCount - innerCount;

        PlacePlanets(rng, types, innerCount, minOrbit,         beltInner * 0.9f);
        PlacePlanets(rng, types, outerCount, beltOuter * 1.1f, maxOrbit);
    }

    private void PlacePlanets(Random rng, PlanetType[] types, int count,
                              float minOrbit, float maxOrbit)
    {
        for (int i = 0; i < count; i++)
        {
            var   type   = types[rng.Next(types.Length)];
            int   seed   = rng.Next();
            float radius = MathHelper.Lerp(MinPlanetRadius, MaxPlanetRadius, (float)rng.NextDouble());

            string id  = $"planet_{Planets.Count}";
            var    tex = PlanetTextureGenerator.Generate(Launcher.GD, type, seed);
            Launcher.TextureCache.Register(id, tex);

            float orbit = MathHelper.Lerp(minOrbit, maxOrbit,
                                          (float)(i + 0.5f + rng.NextDouble() * 0.5f - 0.25f) / count);
            orbit = Math.Clamp(orbit, minOrbit, maxOrbit);
            float angle = (float)(rng.NextDouble() * MathHelper.TwoPi);

            var planet = new Planet
            {
                TextureId    = id,
                Radius       = radius,
                Name         = PlanetNames[Planets.Count % PlanetNames.Length],
                MinimapColor = PlanetMinimapColor(type),
            };
            planet.Transform.Position = new Vector2(
                (float)Math.Cos(angle) * orbit,
                (float)Math.Sin(angle) * orbit);
            planet.Transform.Scale = 1f;

            Planets.Add(planet);
        }
    }

    private static Color PlanetMinimapColor(PlanetType type) => type switch
    {
        PlanetType.Terran   => new Color( 70, 160, 220),
        PlanetType.Rocky    => new Color(160, 130, 100),
        PlanetType.GasGiant => new Color(210, 170, 100),
        PlanetType.Ice      => new Color(200, 225, 255),
        PlanetType.Lava     => new Color(220,  70,  30),
        PlanetType.Ocean    => new Color( 30, 100, 200),
        _                   => Color.LightGray,
    };

    // ── Asteroids ───────────────────────────────────────────────────────────

    private void GenerateAsteroids(Random rng)
    {
        // Pre-generate a small palette of asteroid textures and reuse them
        const int PaletteSize = 8;
        var paletteIds = new string[PaletteSize];
        for (int i = 0; i < PaletteSize; i++)
        {
            paletteIds[i] = $"asteroid_tex_{i}";
            var tex = AsteroidTextureGenerator.Generate(Launcher.GD, rng.Next());
            Launcher.TextureCache.Register(paletteIds[i], tex);
        }

        float beltInner = AsteroidBeltInnerRadius;
        float beltOuter = AsteroidBeltOuterRadius;

        for (int i = 0; i < AsteroidCount; i++)
        {
            float orbit = MathHelper.Lerp(beltInner, beltOuter, (float)rng.NextDouble());
            float angle = (float)(rng.NextDouble() * MathHelper.TwoPi);

            float radius = MathHelper.Lerp(MinAsteroidRadius, MaxAsteroidRadius,
                                           (float)rng.NextDouble());

            var asteroid = new Asteroid
            {
                TextureId = paletteIds[rng.Next(PaletteSize)],
                Radius    = radius,
            };

            asteroid.Transform.Position = new Vector2(
                (float)Math.Cos(angle) * orbit,
                (float)Math.Sin(angle) * orbit);
            asteroid.Transform.Rotation = (float)(rng.NextDouble() * MathHelper.TwoPi);

            float speed     = MathHelper.Lerp(8f, 30f, (float)rng.NextDouble());
            float perpAngle = angle + MathHelper.PiOver2;
            asteroid.Physics.Velocity = new Vector2(
                (float)Math.Cos(perpAngle) * speed,
                (float)Math.Sin(perpAngle) * speed);
            asteroid.Physics.AngularVelocity = MathHelper.Lerp(-0.4f, 0.4f, (float)rng.NextDouble());

            Asteroids.Add(asteroid);
        }
    }

    // ── Background stars ──────────────────────────────────────────────────

    private void GenerateBackgroundStars(Random rng)
    {
        // Positions are in a virtual 2048×2048 space used only for parallax scrolling
        const float VirtualSize = 2048f;
        for (int i = 0; i < BackgroundStarCount; i++)
        {
            BackgroundStars.Add(new BackgroundStar
            {
                Position   = new Vector2(
                    (float)rng.NextDouble() * VirtualSize,
                    (float)rng.NextDouble() * VirtualSize),
                Brightness = MathHelper.Lerp(0.35f, 1f, (float)rng.NextDouble()),
                Size       = rng.NextDouble() < 0.15 ? 2f : 1f,
                // Randomly placed in front of (layer 1) or behind (layer 3) the nebula
                Layer      = rng.NextDouble() < 0.5 ? 1 : 3,
            });
        }
    }

    // ── Nebula ───────────────────────────────────────────────────────────────

    private void GenerateNebula(Random rng, int universeSeedHash)
    {
        const string id = "nebula";
        var tex = NebulaGenerator.Generate(universeSeedHash + 77777);
        Launcher.TextureCache.Register(id, tex);
        NebulaTextureId = id;
        // Cover the full system plus a comfortable margin so the player never
        // flies off the edge of the nebula at any practical zoom level.
        NebulaWorldSize = SystemRadius * 2.4f;
    }
}
