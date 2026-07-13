using Microsoft.Xna.Framework;
using StrangeUniverse.Game.Components;
using StrangeUniverse.Rendering.ProceduralGeneration;
using System;
using static StrangeUniverse.StaticHelpers;

namespace StrangeUniverse.Game.Entities;

public class Planet
{
    public Transform Transform    { get; } = new();
    public float     Radius       { get; set; }
    public string    Name         { get; set; } = string.Empty;
    public PlanetType Type { get; set;  }
    public string Id { get; }
    public Planet(string planetId, float minPlanetRadius, float maxPlanetRadius, float minOrbit, float maxOrbit, int planetNumber, int totalPlanets)
    {
        Id = planetId;
        Random planetRng = new Random(StaticHelpers.SeedHash(Id));
        Name = StaticHelpers.PlanetNames[planetRng.Next(StaticHelpers.PlanetNames.Length-1)];

        Type = PlanetTypes[planetRng.Next(PlanetTypes.Length)];  
        Radius = MathHelper.Lerp(minPlanetRadius, maxPlanetRadius, (float)planetRng.NextDouble());

        var tex = PlanetTextureGenerator.Generate(Launcher.GD, Type, StaticHelpers.SeedHash(Id));
        Launcher.TextureCache.Register(Id, tex);

        float orbit = MathHelper.Lerp(minOrbit, maxOrbit,
                                      (float)(planetNumber + 0.5f + planetRng.NextDouble() * 0.5f - 0.25f) / totalPlanets);
        orbit = Math.Clamp(orbit, minOrbit, maxOrbit);
        float angle = (float)(planetRng.NextDouble() * MathHelper.TwoPi);

        Transform.Position = new Vector2(
            (float)Math.Cos(angle) * orbit,
            (float)Math.Sin(angle) * orbit);
        Transform.Scale = 1f;
    }
}
