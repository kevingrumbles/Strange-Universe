using Microsoft.Xna.Framework;
using Strange_Universe;
using Strange_Universe.Game.Entities;
using Strange_Universe.Game.Components;
using System;
using System.Text.Json.Serialization;
using static Strange_Universe.NameGenerator;

namespace Strange_Universe.Game.Entities;

public class Planet
{
    private Transform Transform    { get; } = new();
    /// <summary>Current ship position in world space.</summary>
    [JsonIgnore]
    public Vector2 Position
    {
        get => Transform.Position;
        set => Transform.Position = value;
    }

    /// <summary>Current ship heading (rotation in radians).</summary>
    [JsonIgnore]
    public float Rotation
    {
        get => Transform.Rotation;
        set => Transform.Rotation = value;
    }

    [JsonIgnore]
    public float Scale
    {
        get => Transform.Scale;
        set => Transform.Scale = value;
    }

    /// <summary>Forward direction vector based on current rotation.</summary>
    [JsonIgnore]
    public Vector2 Forward
    {
        get => Transform.Forward;
    }

    public float     Radius       { get; set; }
    public string    Name         { get; set; } = string.Empty;
    public PlanetType Type { get; set;  }
    public string Id { get; }
    public GravityWell GravityWell { get; private set; }

    public Planet(string planetId, float minPlanetRadius, float maxPlanetRadius, float minOrbit, float maxOrbit, int planetNumber, int totalPlanets)
    {
        Id = planetId;
        Random planetRng = new Random(ProceduralHelpers.SeedHash(Id));
        Name = NameGenerator.GenerateCelestialName(CelestialNameType.Planet, random: planetRng);

        int PlanetTypes = ((PlanetType[])Enum.GetValues(typeof(PlanetType))).Length;
        Type = (PlanetType)planetRng.Next(PlanetTypes);  
        Radius = MathHelper.Lerp(minPlanetRadius, maxPlanetRadius, (float)planetRng.NextDouble());

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

}
