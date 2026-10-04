using Microsoft.Xna.Framework;
using Strange_Universe.Game.Entities;
using Strange_Universe.Game.Components;
using System;
using System.Text.Json.Serialization;

namespace Strange_Universe.Game.Entities;

public class Star
{
    public string    Id { get; }
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
    public string    Name         { get; set; }
    public float     OrbitRadius  { get; set; }
    public float     OrbitAngle   { get; set; }
    public float     OrbitSpeed   { get; set; }
    /// <summary>Index into <c>ProceduralHelpers.StarColors</c>; mapped to a colour on the render side.</summary>
    public int       ColorIndex   { get; set; }
    public GravityWell GravityWell { get; private set; }

    public Star(string id, float radius, string name, int colorIndex, float orbitRadius, float orbitAngle, float orbitSpeed)
    {
        Id = id;
        Radius = radius;
        Name = name;
        ColorIndex = colorIndex;
        OrbitRadius = orbitRadius;
        OrbitAngle = orbitAngle;
        OrbitSpeed = orbitSpeed;
        Random starRng = new Random(ProceduralHelpers.SeedHash($"{Id}_{Name}"));


        // Create gravity well based on star size
        float wellRadius = Radius * MathHelper.Lerp(3f, 6f, (float)starRng.NextDouble());
        // Stars use 80-100% of max gravity force at their center
        float wellStrength = MathHelper.Lerp(0.8f, 1.0f, (float)starRng.NextDouble());
        GravityWell = new GravityWell(Transform.Position, wellRadius, wellStrength);
    }

    /// <summary>
    /// Returns the RNG in the exact state the texture generator previously received
    /// (after the two gravity-well draws), so star textures are unchanged.
    /// </summary>
    public Random CreateTextureRandom()
    {
        var rng = new Random(ProceduralHelpers.SeedHash($"{Id}_{Name}"));
        rng.NextDouble();
        rng.NextDouble();
        return rng;
    }
}
