using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Strange_Universe;
using StrangeUniverse.Game.Components;
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
    public GravityWell GravityWell { get; private set; }

    private const int Size = 256;
    public Planet(string planetId, float minPlanetRadius, float maxPlanetRadius, float minOrbit, float maxOrbit, int planetNumber, int totalPlanets)
    {
        Id = planetId;
        Random planetRng = new Random(StaticHelpers.SeedHash(Id));
        Name = StaticHelpers.GenerateCelestialName(StaticHelpers.CelestialNameType.Planet, random: planetRng);

        int PlanetTypes = ((PlanetType[])Enum.GetValues(typeof(PlanetType))).Length;
        Type = (PlanetType)planetRng.Next(PlanetTypes);  
        Radius = MathHelper.Lerp(minPlanetRadius, maxPlanetRadius, (float)planetRng.NextDouble());

        var tex = Generate(Launcher.GD, Type, StaticHelpers.SeedHash(Id));
        Launcher.TextureCache.Register(Id, tex);

        float orbit = MathHelper.Lerp(minOrbit, maxOrbit,
                                      (float)(planetNumber + 0.5f + planetRng.NextDouble() * 0.5f - 0.25f) / totalPlanets);
        orbit = Math.Clamp(orbit, minOrbit, maxOrbit);
        float angle = (float)(planetRng.NextDouble() * MathHelper.TwoPi);

        Transform.Position = new Vector2(
            (float)Math.Cos(angle) * orbit,
            (float)Math.Sin(angle) * orbit);
        Transform.Scale = 1f;

        // Create gravity well based on planet type and size
        // Maximum gravity force is a percentage of player thrust (dynamic)
        // Different planet types have different densities affecting their gravity strength
        // Lava planets have the highest density (1.0x), others are scaled down from there

        float densityMultiplier = Type switch
        {
            PlanetType.Lava => 1.0f,      // Highest density (baseline)
            PlanetType.Terran => 0.92f,   // High density
            PlanetType.Ocean => 0.85f,    // Medium-high density
            PlanetType.Rocky => 0.77f,    // Medium density
            PlanetType.Ice => 0.69f,      // Low-medium density
            PlanetType.GasGiant => 0.31f, // Lowest density
            _ => 0.77f
        };

        // Well radius is 8-15x the planet's visual radius
        // Planets are weaker than stars (30-60% of max force at center) and vary by density
        float wellRadius = Radius * MathHelper.Lerp(8f, 15f, (float)planetRng.NextDouble());
        float wellStrength = MathHelper.Lerp(0.3f, 0.6f, (float)planetRng.NextDouble()) * densityMultiplier;
        GravityWell = new GravityWell(Transform.Position, wellRadius, wellStrength);
    }

    public static Texture2D Generate(GraphicsDevice gd, PlanetType type, int seed)
    {
        var colors = new Color[Size * Size];
        float cx = Size * 0.5f;
        float cy = Size * 0.5f;
        float r = cx - 2;

        // Light direction (slightly above left)
        var light = Vector3.Normalize(new Vector3(-0.4f, -0.6f, 0.8f));

        for (int py = 0; py < Size; py++)
        {
            for (int px = 0; px < Size; px++)
            {
                float dx = px - cx;
                float dy = py - cy;
                float d2 = dx * dx + dy * dy;
                if (d2 > r * r) continue;

                float dist = (float)Math.Sqrt(d2);
                float normDist = dist / r;

                // Sphere normal (Lambert)
                float nz = (float)Math.Sqrt(Math.Max(0f, 1f - normDist * normDist));
                var normal = Vector3.Normalize(new Vector3(dx / r, dy / r, nz));
                float diffuse = Math.Max(0f, Vector3.Dot(normal, light));
                float ambient = 0.25f;
                float lighting = ambient + (1f - ambient) * diffuse;

                // Sample noise for surface detail
                float noiseScale = 3.5f;
                float nx = (dx / r + 1f) * noiseScale;
                float ny = (dy / r + 1f) * noiseScale;
                float n = ProceduralHelpers.Remap01(ProceduralHelpers.Fbm(nx, ny, seed, 6, 0.5f, 2f));

                // Atmosphere edge glow
                float edgeFactor = 1f - normDist;

                Color surfaceColor = ProceduralHelpers.GetSurfaceColor(type, n, py, seed);

                // Darken surface by lighting
                surfaceColor = new Color(
                    (int)(surfaceColor.R * lighting),
                    (int)(surfaceColor.G * lighting),
                    (int)(surfaceColor.B * lighting));

                // Atmospheric rim
                Color atmColor = ProceduralHelpers.GetAtmosphereColor(type);
                float rimStrength = (float)Math.Pow(1f - edgeFactor, 4f);
                surfaceColor = Color.Lerp(surfaceColor, atmColor, rimStrength * 0.7f);

                // Planet body is fully opaque; only the outermost 5% fades for a soft edge
                byte alpha = normDist < 0.95f
                    ? (byte)255
                    : (byte)(Math.Max(0f, 1f - (normDist - 0.95f) / 0.05f) * 255f);
                colors[py * Size + px] = new Color(surfaceColor.R, surfaceColor.G, surfaceColor.B, alpha);
            }
        }

        var tex = new Texture2D(gd, Size, Size);
        tex.SetData(colors);
        return tex;
    }
}
