using Microsoft.Xna.Framework;
using Strange_Universe.Game.Entities;
using Strange_Universe.Game.Components;
using System;
using System.Text.Json.Serialization;

namespace Strange_Universe.Game.Entities;

public class Star : Transform
{
    public string    Id { get; }

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
        GravityWell = new GravityWell(Position, wellRadius, wellStrength);
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
