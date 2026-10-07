using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using Strange_Universe.Game.Components;

namespace Strange_Universe.Game.Entities;

/// <summary>
/// Pure generation of a <see cref="StarSystemLayout"/> from a node's seed. It reads nothing from the
/// universe or galaxy and requests no assets. The order of Random draws is load-bearing:
/// changing it changes every generated system.
/// </summary>
public static class StarSystemGenerator
{
    /// <summary>Number of shared asteroid palette textures.</summary>
    public const int AsteroidPaletteSize = 15;

    public static string AsteroidPaletteId(int index) => $"asteroid_tex_{index}";

    public static StarSystemLayout Generate(StarSystemNode node, int nebulaPoolSize)
    {
        ArgumentNullException.ThrowIfNull(node);

        var layout = DeriveParameters(node, new Random(ProceduralHelpers.SeedHash(node.SystemId)));
        return layout with
        {
            Stars = GenerateStars(node.SystemId, layout),
            Planets = GeneratePlanets(node.SystemId, layout),
            Asteroids = GenerateAsteroids(node.SystemId, layout),
            BackgroundStars = GenerateBackgroundStars(node.SystemId, layout),
            NebulaIndex = NebulaIndexFor(node.SystemId, nebulaPoolSize),
        };
    }

    private static StarSystemLayout DeriveParameters(StarSystemNode node, Random rng)
    {
        int planetCount = rng.Next(0, 7);
        int asteroidCount = rng.Next(0, 120);
        int starCount = rng.Next(1, 3);
        float starRadius = rng.NextWeightedFloat(250f, 500f);
        float starOrbitRadius = 0f;
        float starOrbitSpeed = 0f;
        switch (starCount)
        {
            case 2:
                starOrbitRadius = rng.NextWeightedFloat(2000f, 4000f);
                starOrbitSpeed  = rng.NextWeightedFloat(0.0001f, 0.0003f);
                break;
            case 3:
                starOrbitRadius = rng.NextWeightedFloat(3000f, 7000f);
                starOrbitSpeed  = rng.NextWeightedFloat(0.00005f, 0.0002f);
                break;
        }

        float minPlanetRadius = rng.NextWeightedFloat(90f, 120f);
        float maxPlanetRadius = rng.NextWeightedFloat(150, 300f);
        float minAsteroidRadius = rng.NextWeightedFloat(15f, 30f);
        float maxAsteroidRadius = rng.NextWeightedFloat(40f, 50f);
        int connectionCount = rng.Next(1, 4);
        if (connectionCount == 1) connectionCount = rng.Next(1, 4);
        int innerPlanetCount = rng.Next(planetCount);
        int backgroundStarCount = rng.Next(400, 800);

        if (node.IsHome)
        {
            innerPlanetCount = 4;
            planetCount = 8;
            connectionCount = 4;
            starOrbitRadius = 0f;
            starOrbitSpeed = 0f;
            starCount = 1;
            asteroidCount = 100;
        }

        // The star core is the region occupied by all orbiting stars.
        // For a single star StarOrbitRadius == 0, so starCoreRadius == StarRadius.
        float starCoreRadius = starOrbitRadius + starRadius;
        // SystemRadius is always large enough to contain the star core plus a meaningful planetary region.
        float starCoreFootprint = starOrbitRadius * 2f;
        float systemRadius = rng.NextWeightedFloat(12000f + starCoreFootprint, 25000f + starCoreFootprint);

        float beltInner = Math.Max(
            rng.NextWeightedFloat(systemRadius * 0.2f, systemRadius * 0.4f),
            starCoreRadius * 2.5f);
        float beltOuter = rng.NextWeightedFloat(beltInner * 1.2f, systemRadius * 0.6f);

        return new StarSystemLayout
        {
            PlanetCount = planetCount,
            AsteroidCount = asteroidCount,
            StarCount = starCount,
            StarRadius = starRadius,
            StarOrbitRadius = starOrbitRadius,
            StarOrbitSpeed = starOrbitSpeed,
            MinPlanetRadius = minPlanetRadius,
            MaxPlanetRadius = maxPlanetRadius,
            MinAsteroidRadius = minAsteroidRadius,
            MaxAsteroidRadius = maxAsteroidRadius,
            SystemConnectionCount = connectionCount,
            InnerPlanetCount = innerPlanetCount,
            BackgroundStarCount = backgroundStarCount,
            SystemRadius = systemRadius,
            MandevilleRadius = systemRadius * 0.75f,
            AsteroidBeltInnerRadius = beltInner,
            AsteroidBeltOuterRadius = beltOuter,
        };
    }

    private static List<Star> GenerateStars(string systemId, StarSystemLayout l)
    {
        var stars = new List<Star>();
        for (int i = 0; i < l.StarCount; i++)
        {
            string starId = $"{systemId}_Star_{i}";
            Random starRandom = new Random(ProceduralHelpers.SeedHash(starId));
            int colorIndex = starRandom.Next(ProceduralHelpers.StarColors.Length);
            string name = null;
            while (name is null || stars.Any(st => st.Name == name))
            {
                name = NameGenerator.GenerateCelestialName(CelestialNameType.Star, random: starRandom);
            }

            float initialAngle = MathHelper.TwoPi * i / l.StarCount;
            Star newStar = new Star(starId, l.StarRadius, name, colorIndex, l.StarOrbitRadius, initialAngle, l.StarOrbitSpeed);
            newStar.Position = l.StarCount == 1
                ? Vector2.Zero
                : new Vector2(
                    MathF.Cos(initialAngle) * l.StarOrbitRadius,
                    MathF.Sin(initialAngle) * l.StarOrbitRadius);
            stars.Add(newStar);
        }
        return stars;
    }

    private static List<Planet> GeneratePlanets(string systemId, StarSystemLayout l)
    {
        var planets = new List<Planet>();
        for (int i = 1; i <= l.PlanetCount; i++)
        {
            bool inner = i <= l.InnerPlanetCount;
            planets.Add(new Planet(planetId: $"{systemId}_Planet_{i}",
                                   minPlanetRadius: l.MinPlanetRadius,
                                   maxPlanetRadius: l.MaxPlanetRadius,
                                   minOrbit: inner ? Math.Max(l.StarRadius * 3.5f, l.StarOrbitRadius * 2f) : l.AsteroidBeltOuterRadius * 1.1f,
                                   maxOrbit: inner ? l.AsteroidBeltInnerRadius * 0.9f : l.SystemRadius * 0.75f,
                                   planetNumber: i,
                                   totalPlanets: l.PlanetCount));
        }
        return planets;
    }

    private static List<Asteroid> GenerateAsteroids(string systemId, StarSystemLayout l)
    {
        Random asteroidsRng = new Random(ProceduralHelpers.SeedHash($"{systemId}_Asteroids"));

        // Always consume one draw per palette slot so placement below never depends on
        // which textures already exist (visit order) or whether assets are attached.
        for (int i = 0; i < AsteroidPaletteSize; i++)
            asteroidsRng.Next();

        var asteroids = new List<Asteroid>();
        for (int i = 0; i < l.AsteroidCount; i++)
        {
            float orbit = MathHelper.Lerp(l.AsteroidBeltInnerRadius, l.AsteroidBeltOuterRadius, (float)asteroidsRng.NextDouble());
            float angle = (float)(asteroidsRng.NextDouble() * MathHelper.TwoPi);
            float radius = MathHelper.Lerp(l.MinAsteroidRadius, l.MaxAsteroidRadius, (float)asteroidsRng.NextDouble());
            string textureId = AsteroidPaletteId(asteroidsRng.Next(AsteroidPaletteSize));
            asteroids.Add(new Asteroid(angle, orbit, radius, textureId, asteroidsRng));
        }
        return asteroids;
    }

    private static List<BackgroundStar> GenerateBackgroundStars(string systemId, StarSystemLayout l)
    {
        Random rng = new Random(ProceduralHelpers.SeedHash($"{systemId}_BackgroundStars"));

        // Positions are in virtual space matching the parallax tile size used by the renderer.
        const float VirtualWidth = BackgroundTile.Width;
        const float VirtualHeight = BackgroundTile.Height;

        var stars = new List<BackgroundStar>();
        for (int i = 0; i < l.BackgroundStarCount; i++)
        {
            stars.Add(new BackgroundStar
            {
                Position   = new Vector2(
                    (float)rng.NextDouble() * VirtualWidth,
                    (float)rng.NextDouble() * VirtualHeight),
                Brightness = MathHelper.Lerp(0.35f, 1f, (float)rng.NextDouble()),
                Size       = rng.NextDouble() < 0.15 ? 2f : 1f,
                // Randomly placed in front of (layer 1) or behind (layer 0) the nebula
                Layer      = rng.NextDouble() < 0.5 ? 1 : 0,
            });
        }
        return stars;
    }

    /// <summary>
    /// Pool slot used by the system <paramref name="systemId"/>. Pure, so the universe can
    /// prioritize loading that nebula before the system is built. The slot comes from the fixed pool
    /// size, never the current fill level, which depends on background generation timing.
    /// </summary>
    public static int NebulaIndexFor(string systemId, int nebulaPoolSize) =>
        new Random(ProceduralHelpers.SeedHash($"{systemId}_Nebula")).Next(nebulaPoolSize);
}
