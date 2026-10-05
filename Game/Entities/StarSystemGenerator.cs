using Microsoft.Xna.Framework;
using System;
using System.Diagnostics;
using System.Linq;
using Strange_Universe.Game.Components;

namespace Strange_Universe.Game.Entities;

/// <summary>
/// Procedurally fills a <see cref="StarSystem"/> from its node's seed.
/// The order of Random draws is load-bearing: changing it changes every generated system.
/// </summary>
public static class StarSystemGenerator
{
    public static void Populate(StarSystem system)
    {
        var sw = Stopwatch.StartNew();
        StarSystemNode node = system.Node;

        DeriveParameters(system, new Random(ProceduralHelpers.SeedHash(node.SystemId)));
        Debug.WriteLine($"properties loaded: {sw.ElapsedMilliseconds} ms");

        GenerateStars(system);
        GeneratePlanets(system);
        GenerateAsteroids(system);
        GenerateBackgroundStars(system);
        SelectNebula(system);
        Debug.WriteLine($"Generating Connections: {sw.ElapsedMilliseconds} ms");
        system.Universe.Galaxy.GenerateConnections(node, system.SystemConnectionCount, system.Universe.Seed);
        Debug.WriteLine($"StarSystem generation completed: {sw.ElapsedMilliseconds} ms");
    }

    private static void DeriveParameters(StarSystem s, Random rng)
    {
        s.PlanetCount = rng.Next(0, 7);
        s.AsteroidCount = rng.Next(0, 120);
        s.StarCount = rng.Next(1, 3);
        s.StarRadius = rng.NextWeightedFloat(250f, 500f);
        switch (s.StarCount)
        {
            case 1:
                s.StarOrbitRadius = 0f;
                s.StarOrbitSpeed = 0f;
                break;
            case 2:
                s.StarOrbitRadius = rng.NextWeightedFloat(2000f, 4000f);
                s.StarOrbitSpeed  = rng.NextWeightedFloat(0.0001f, 0.0003f);
                break;
            case 3:
                s.StarOrbitRadius = rng.NextWeightedFloat(3000f, 7000f);
                s.StarOrbitSpeed  = rng.NextWeightedFloat(0.00005f, 0.0002f);
                break;
        }

        s.MinPlanetRadius = rng.NextWeightedFloat(90f, 120f);
        s.MaxPlanetRadius = rng.NextWeightedFloat(150, 300f);
        s.MinAsteroidRadius = rng.NextWeightedFloat(15f, 30f);
        s.MaxAsteroidRadius = rng.NextWeightedFloat(40f, 50f);
        s.SystemConnectionCount = rng.Next(1, 4);
        if (s.SystemConnectionCount == 1) s.SystemConnectionCount = rng.Next(1, 4);
        s.InnerPlanetCount = rng.Next(s.PlanetCount);
        s.BackgroundStarCount = rng.Next(400, 800);

        if (s.Node.Name == "Sol")
        {
            s.InnerPlanetCount = 4;
            s.PlanetCount = 8;
            s.SystemConnectionCount = 4;
            s.StarOrbitRadius = 0f;
            s.StarOrbitSpeed = 0f;
            s.StarCount = 1;
            s.AsteroidCount = 100;
        }

        // The star core is the region occupied by all orbiting stars.
        // For a single star StarOrbitRadius == 0, so starCoreRadius == StarRadius.
        float starCoreRadius = s.StarOrbitRadius + s.StarRadius;
        // SystemRadius is always large enough to contain the star core plus a meaningful planetary region.
        float starCoreFootprint = s.StarOrbitRadius * 2f;
        s.SystemRadius = rng.NextWeightedFloat(12000f + starCoreFootprint, 25000f + starCoreFootprint);
        s.MandevilleRadius = s.SystemRadius * 0.75f;

        s.AsteroidBeltInnerRadius = Math.Max(
            rng.NextWeightedFloat(s.SystemRadius * 0.2f, s.SystemRadius * 0.4f),
            starCoreRadius * 2.5f);
        s.AsteroidBeltOuterRadius = rng.NextWeightedFloat(s.AsteroidBeltInnerRadius * 1.2f, s.SystemRadius * 0.6f);
    }

    private static void GenerateStars(StarSystem s)
    {
        for (int i = 0; i < s.StarCount; i++)
        {
            string starId = $"{s.SystemId}_Star_{i}";
            Random starRandom = new Random(ProceduralHelpers.SeedHash(starId));
            int colorIndex = starRandom.Next(ProceduralHelpers.StarColors.Length);
            string name = null;
            while (name is null || s.Stars.Any(st => st.Name == name))
            {
                name = NameGenerator.GenerateCelestialName(CelestialNameType.Star, random: starRandom);
            }

            float initialAngle = MathHelper.TwoPi * i / s.StarCount;
            Star newStar = new Star(starId, s.StarRadius, name, colorIndex, s.StarOrbitRadius, initialAngle, s.StarOrbitSpeed);
            s.Assets?.EnsureStarTexture(newStar);
            newStar.Position = s.StarCount == 1
                ? Vector2.Zero
                : new Vector2(
                    MathF.Cos(initialAngle) * s.StarOrbitRadius,
                    MathF.Sin(initialAngle) * s.StarOrbitRadius);
            s.Stars.Add(newStar);
        }
    }

    private static void GeneratePlanets(StarSystem s)
    {
        for (int i = 1; i <= s.PlanetCount; i++)
        {
            bool inner = i <= s.InnerPlanetCount;
            var planet = new Planet(planetId: $"{s.SystemId}_Planet_{i}",
                                    minPlanetRadius: s.MinPlanetRadius,
                                    maxPlanetRadius: s.MaxPlanetRadius,
                                    minOrbit: inner ? Math.Max(s.StarRadius * 3.5f, s.StarOrbitRadius * 2f) : s.AsteroidBeltOuterRadius * 1.1f,
                                    maxOrbit: inner ? s.AsteroidBeltInnerRadius * 0.9f : s.SystemRadius * 0.75f,
                                    planetNumber: i,
                                    totalPlanets: s.PlanetCount);
            s.Planets.Add(planet);
            s.Assets?.EnsurePlanetTexture(planet);
        }
    }

    private static void GenerateAsteroids(StarSystem s)
    {
        // Pre-generate a small palette of asteroid textures and reuse them
        Random asteroidsRng = new Random(ProceduralHelpers.SeedHash($"{s.SystemId}_Asteroids"));
        const int PaletteSize = 15;
        var paletteIds = new string[PaletteSize];
        for (int i = 0; i < PaletteSize; i++)
        {
            paletteIds[i] = $"asteroid_tex_{i}";

            // Always consume one draw per palette slot so placement below never depends on
            // which textures already exist (visit order) or whether assets are attached.
            asteroidsRng.Next();

            // Palette art is a universe-wide resource, so its seed comes from the universe seed,
            // not from whichever system happens to request it first.
            s.Assets?.EnsureAsteroidTexture(paletteIds[i],
                ProceduralHelpers.SeedHash($"{s.Universe.Seed}_asteroid_tex_{i}"));
        }

        for (int i = 0; i < s.AsteroidCount; i++)
        {
            float orbit = MathHelper.Lerp(s.AsteroidBeltInnerRadius, s.AsteroidBeltOuterRadius, (float)asteroidsRng.NextDouble());
            float angle = (float)(asteroidsRng.NextDouble() * MathHelper.TwoPi);
            float radius = MathHelper.Lerp(s.MinAsteroidRadius, s.MaxAsteroidRadius, (float)asteroidsRng.NextDouble());
            string textureId = paletteIds[asteroidsRng.Next(PaletteSize)];
            s.Asteroids.Add(new Asteroid(angle, orbit, radius, textureId, asteroidsRng));
        }
    }

    private static void GenerateBackgroundStars(StarSystem s)
    {
        Random rng = new Random(ProceduralHelpers.SeedHash($"{s.SystemId}_BackgroundStars"));

        // Positions are in virtual space matching the parallax tile size used by the renderer.
        const float VirtualWidth = BackgroundTile.Width;
        const float VirtualHeight = BackgroundTile.Height;

        for (int i = 0; i < s.BackgroundStarCount; i++)
        {
            s.BackgroundStars.Add(new BackgroundStar
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
    }

    private static void SelectNebula(StarSystem s)
    {
        Random nebulaRandom = new Random(ProceduralHelpers.SeedHash($"{s.SystemId}_Nebula"));

        // Index into the fixed pool size (not the current fill level, which depends on background
        // generation timing). The renderer draws nothing until that nebula's texture is uploaded.
        int index = nebulaRandom.Next(Universe.NebulaPoolSize);
        s.NebulaId = Universe.NebulaPoolId(s.Universe.Seed, index);
    }
}
