using Microsoft.Xna.Framework;
using Strange_Universe;
using Strange_Universe.Game.Entities;
using Strange_Universe.Game.Components;
using Strange_Universe.Game.Components.PlanetComponents;
using Strange_Universe.Game.Entities.ShipParts;
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

    /// <summary>What a docked ship can do here; null when the planet has no spaceport and cannot be docked at. Never set on a gas giant.</summary>
    public Spaceport Spaceport { get; }

    /// <summary>The chance that a planet that can have a spaceport (anything but a gas giant) has one.</summary>
    public const float SpaceportChance = 0.5f;

    /// <summary>How close to the planet's surface (not its centre) a ship has to be to dock.</summary>
    public const float DockRange = 300f;

    /// <summary>The fastest a ship can be moving and still dock, in units per second: stopped, or almost.</summary>
    public const float MaxDockSpeed = 5f;

    /// <summary>
    /// Whether <paramref name="ship"/> can dock here now. The planet decides this alone: it needs a spaceport, the ship within <see cref="DockRange"/>
    /// of its surface and moving no faster than <see cref="MaxDockSpeed"/>. Range is checked before speed.
    /// </summary>
    public DockingCheck CheckDocking(Ship ship)
    {
        if (Spaceport == null)
            return DockingCheck.NoSpaceport;
        if (Vector2.Distance(ship.Position, Position) - Radius > DockRange)
            return DockingCheck.OutOfRange;
        if (ship.Speed > MaxDockSpeed)
            return DockingCheck.TooFast;
        return DockingCheck.Ok;
    }

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

        Spaceport = RollSpaceport();
    }

    /// <summary>
    /// A planet other than a gas giant has a spaceport with probability <see cref="SpaceportChance"/>. The roll has its own seeded generator, so it
    /// does not disturb the draws that generate the planet itself.
    /// </summary>
    private Spaceport RollSpaceport()
    {
        if (Type == PlanetType.GasGiant)
            return null;

        var rng = new Random(ProceduralHelpers.SeedHash($"{Id}_spaceport"));
        if (rng.NextDouble() >= SpaceportChance)
            return null;

        return new Spaceport("Landing pads, a fuel dock and a repair yard.",
            new SpaceportService[] { new RefuelService(), new RepairService(), new UndockService() });
    }

}
