using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Strange_Universe.Game.Entities;

public class ShipStats
{
    public static List<ShipStats> Presets { get; set; } = new();
    public string ShipName { get; set; }

    public string SpriteName { get; set; }
    public float ThrustForce    { get; set; }
    public float RotationSpeed  { get; set; }
    public float MaxSpeed       { get; set; }
    /// <summary>1.0 = true vacuum (no passive friction). Do not set below 1.0 for ships.</summary>
    public float LinearDamping  { get; set; }
    public float Radius         { get; set; }
    /// <summary>Fraction of MaxSpeed at which the forward thrust soft-cap begins (0-1).</summary>
    public float SoftCapStart   { get; set; }
    /// <summary>
    /// Radians added to the sprite's rotation before drawing.
    /// Use -1.5708 (≈ -π/2) if the sprite points upward; 0 if it already points right (+X).
    /// </summary>
    public float SpriteRotationOffset { get; set; }
    /// <summary>
    /// Scale multiplier applied to the sprite when rendering.
    /// Default is 1.0. Values > 1.0 make the sprite larger, < 1.0 make it smaller.
    /// </summary>
    public float SpriteScale { get; set; } = 1.0f;
    public int MaxHull { get; set; }
    public int MaxShield { get; set; }
    public int MaxFuel { get; set; }
    public float Mass { get; set; } = 1.0f;
    public ShipStats GetShipStats(string name)
    {
        return Presets.Find(s => s.ShipName == name) ?? throw new KeyNotFoundException($"ShipStats preset '{name}' not found.");
    }
}
