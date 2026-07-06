using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StrangeUniverse.Game.Entities;
using StrangeUniverse.Rendering.ProceduralGeneration;
using StrangeUniverse.Rendering.SpriteRenderer;

namespace StrangeUniverse.Game.World;

/// <summary>
/// Builds a <see cref="Universe"/> containing a single procedurally generated
/// <see cref="StarSystem"/>. This is the only place that bridges game entities
/// with the rendering/generation layer.
/// </summary>
public class UniverseGenerator
{
    private readonly GraphicsDevice         _gd;
    private readonly ProceduralTextureCache _cache;
    private readonly Universe               _universe;
    private readonly ShipStats              _shipStats;

    private static readonly string[] PlanetNames =
    {
        "Aether", "Boras", "Calyss", "Drevon", "Eston",
        "Fyrath", "Gavorn", "Helix", "Iridia", "Joras"
    };

    public UniverseGenerator(GraphicsDevice gd, ProceduralTextureCache cache,
                              Universe universe, ShipStats shipStats)
    {
        _gd        = gd;
        _cache     = cache;
        _universe  = universe;
        _shipStats = shipStats;
    }

    public Universe Generate()
    {
        var universeRng = new Random(SeedHash(_universe.Seed));

        // Derive a deterministic per-system seed from the universe seed.
        int    systemSeedInt = universeRng.Next();
        string systemSeed    = systemSeedInt.ToString();

        var system = GenerateStarSystem(systemSeed);
        _universe.AddStarSystem(system);
        return _universe;
    }

    /// <summary>
    /// Deterministic 32-bit FNV-1a hash of a string.
    /// Unlike string.GetHashCode(), this returns the same value on every run.
    /// </summary>
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

    private StarSystem GenerateStarSystem(string seed)
    {
        var rng    = new Random(SeedHash(seed));
        var system = new StarSystem { Seed = seed };

        GenerateStar(system, rng);
        GeneratePlanets(system, rng);
        GenerateAsteroids(system, rng);
        GenerateBackgroundStars(system, rng);
        GenerateNebula(system, rng);
        GeneratePlayer(system);

        return system;
    }

    // ── Star ─────────────────────────────────────────────────────────────────

    private void GenerateStar(StarSystem system, Random rng)
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
        var tex = StarTextureGenerator.Generate(_gd, starColor, SeedHash(_universe.Seed));
        _cache.Register(id, tex);

        system.Star = new Star
        {
            TextureId    = id,
            Radius       = _universe.StarRadius,
            Name         = "Sol",
            MinimapColor = starColor,
        };
        system.Star.Transform.Position = Vector2.Zero;
    }

    // ── Planets ───────────────────────────────────────────────────────────────

    private void GeneratePlanets(StarSystem system, Random rng)
    {
        var types = (PlanetType[])Enum.GetValues(typeof(PlanetType));

        // Space planets evenly in the system, skipping the asteroid belt zone
        float minOrbit  = _universe.StarRadius * 3.5f;
        float beltInner = _universe.AsteroidBeltInnerRadius;
        float beltOuter = _universe.AsteroidBeltOuterRadius;
        float maxOrbit  = _universe.SystemRadius * 0.75f;

        // Generate inner planets (before belt) and outer planets (after belt)
        int innerCount = Math.Max(1, _universe.PlanetCount / 2);
        int outerCount = _universe.PlanetCount - innerCount;

        PlacePlanets(system, rng, types, innerCount, minOrbit,        beltInner * 0.9f);
        PlacePlanets(system, rng, types, outerCount, beltOuter * 1.1f, maxOrbit);
    }

    private void PlacePlanets(StarSystem system, Random rng, PlanetType[] types,
                               int count, float minOrbit, float maxOrbit)
    {
        for (int i = 0; i < count; i++)
        {
            var   type   = types[rng.Next(types.Length)];
            int   seed   = rng.Next();
            float radius = MathHelper.Lerp(_universe.MinPlanetRadius,
                                           _universe.MaxPlanetRadius,
                                           (float)rng.NextDouble());

            string id  = $"planet_{system.Planets.Count}";
            var    tex = PlanetTextureGenerator.Generate(_gd, type, seed);
            _cache.Register(id, tex);

            float orbit = MathHelper.Lerp(minOrbit, maxOrbit,
                                          (float)(i + 0.5f + rng.NextDouble() * 0.5f - 0.25f) / count);
            orbit = Math.Clamp(orbit, minOrbit, maxOrbit);
            float angle = (float)(rng.NextDouble() * MathHelper.TwoPi);

            string name = PlanetNames[system.Planets.Count % PlanetNames.Length];

            var planet = new Planet
            {
                TextureId    = id,
                Radius       = radius,
                Name         = name,
                MinimapColor = PlanetMinimapColor(type),
            };
            planet.Transform.Position = new Vector2(
                (float)Math.Cos(angle) * orbit,
                (float)Math.Sin(angle) * orbit);
            planet.Transform.Scale = 1f;

            system.Planets.Add(planet);
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

    // ── Asteroids ─────────────────────────────────────────────────────────────

    private void GenerateAsteroids(StarSystem system, Random rng)
    {
        // Pre-generate a small palette of asteroid textures and reuse them
        const int PaletteSize = 8;
        var paletteIds = new string[PaletteSize];
        for (int i = 0; i < PaletteSize; i++)
        {
            paletteIds[i] = $"asteroid_tex_{i}";
            var tex = AsteroidTextureGenerator.Generate(_gd, rng.Next());
            _cache.Register(paletteIds[i], tex);
        }

        float beltInner = _universe.AsteroidBeltInnerRadius;
        float beltOuter = _universe.AsteroidBeltOuterRadius;

        for (int i = 0; i < _universe.AsteroidCount; i++)
        {
            float orbit = MathHelper.Lerp(beltInner, beltOuter, (float)rng.NextDouble());
            float angle = (float)(rng.NextDouble() * MathHelper.TwoPi);

            float radius = MathHelper.Lerp(_universe.MinAsteroidRadius,
                                           _universe.MaxAsteroidRadius,
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

            // Slow orbital drift
            float speed      = MathHelper.Lerp(8f, 30f, (float)rng.NextDouble());
            float perpAngle  = angle + MathHelper.PiOver2;
            asteroid.Physics.Velocity = new Vector2(
                (float)Math.Cos(perpAngle) * speed,
                (float)Math.Sin(perpAngle) * speed);
            asteroid.Physics.AngularVelocity = MathHelper.Lerp(-0.4f, 0.4f, (float)rng.NextDouble());

            system.Asteroids.Add(asteroid);
        }
    }

    // ── Background stars ──────────────────────────────────────────────────────

    private void GenerateBackgroundStars(StarSystem system, Random rng)
    {
        // Positions are in a virtual 2048×2048 space used only for parallax scrolling
        const float VirtualSize = 2048f;
        for (int i = 0; i < _universe.BackgroundStarCount; i++)
        {
            system.BackgroundStars.Add(new BackgroundStar
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

    // ── Nebula ────────────────────────────────────────────────────────────────

    private void GenerateNebula(StarSystem system, Random rng)
    {
        const string id = "nebula";
        var tex = NebulaGenerator.Generate(_gd, SeedHash(_universe.Seed) + 77777);
        _cache.Register(id, tex);
        system.NebulaTextureId = id;
        // Cover the full system plus a comfortable margin so the player never
        // flies off the edge of the nebula at any practical zoom level.
        system.NebulaWorldSize = _universe.SystemRadius * 2.4f;
    }

    // ── Player ────────────────────────────────────────────────────────────────

    private void GeneratePlayer(StarSystem system)
    {
        const string id         = "player_ship";
        const string spriteFile = "Art/Shuttle_sprite.png";

        var tex = ArtLoader.TryLoad(_gd, spriteFile)
               ?? ShipTextureGenerator.Generate(_gd);

        _cache.Register(id, tex);

        var player = new PlayerShip(_shipStats)
        {
            TextureId = id,
        };
        player.Transform.Position = new Vector2(
            _universe.AsteroidBeltInnerRadius * 0.35f, 0f);

        system.Player = player;
    }
}
