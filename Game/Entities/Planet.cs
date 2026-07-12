using Microsoft.Xna.Framework;
using StrangeUniverse.Game.Components;
using StrangeUniverse.Rendering.ProceduralGeneration;
using System;

namespace StrangeUniverse.Game.Entities;

public class Planet
{
    public Transform Transform    { get; } = new();
    public float     Radius       { get; set; }
    public string    Name         { get; set; } = string.Empty;
    public string    TextureId    { get; set; } = string.Empty;
    public Color     MinimapColor { get; set; } = Color.LightGray;
    private readonly int _seed;
    public Planet(string name, Guid systemId, float minPlanetRadius, float maxPlanetRadius, Random systemRng, PlanetType type, float minOrbit, float maxOrbit, int planetNumber, int totalPlanets)
    {
        Name = name;
        TextureId = $"{systemId.ToString()}_{Name}";

        _seed = systemRng.Next();
        Radius = MathHelper.Lerp(minPlanetRadius, maxPlanetRadius, (float)systemRng.NextDouble());
        MinimapColor = PlanetMinimapColor(type);

        var tex = PlanetTextureGenerator.Generate(Launcher.GD, type, _seed);
        Launcher.TextureCache.Register(TextureId, tex);

        float orbit = MathHelper.Lerp(minOrbit, maxOrbit,
                                      (float)(planetNumber + 0.5f + systemRng.NextDouble() * 0.5f - 0.25f) / totalPlanets);
        orbit = Math.Clamp(orbit, minOrbit, maxOrbit);
        float angle = (float)(systemRng.NextDouble() * MathHelper.TwoPi);

        Transform.Position = new Vector2(
            (float)Math.Cos(angle) * orbit,
            (float)Math.Sin(angle) * orbit);
        Transform.Scale = 1f;
    }
    private static Color PlanetMinimapColor(PlanetType type) => type switch
    {
        PlanetType.Terran => new Color(70, 160, 220),
        PlanetType.Rocky => new Color(160, 130, 100),
        PlanetType.GasGiant => new Color(210, 170, 100),
        PlanetType.Ice => new Color(200, 225, 255),
        PlanetType.Lava => new Color(220, 70, 30),
        PlanetType.Ocean => new Color(30, 100, 200),
        _ => Color.LightGray,
    };
}
