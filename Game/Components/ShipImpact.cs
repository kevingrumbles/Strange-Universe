using Microsoft.Xna.Framework;

namespace Strange_Universe.Game.Components;

/// <summary>
/// A short-lived visual produced when a projectile strikes a ship.
/// A shielded hit renders as a curved glow along the shield surface; an
/// unshielded hit throws hull debris off the impact point.
/// </summary>
public class ShipImpact
{
    /// <summary>True when the shield absorbed the hit and the arc should be drawn.</summary>
    public bool ShieldHit { get; init; }

    /// <summary>World-space point of impact at the moment it occurred.</summary>
    public Vector2 Position { get; init; }

    /// <summary>
    /// Angle from the ship centre to the impact point, used to orient the shield arc.
    /// </summary>
    public float Angle { get; init; }

    /// <summary>Radius of the shield arc, in world pixels.</summary>
    public float Radius { get; init; }

    /// <summary>Angular half-width of the shield arc in radians.</summary>
    public float ArcWidth { get; init; }

    /// <summary>Colour of the shield glow.</summary>
    public Color Color { get; init; }

    /// <summary>Remaining lifetime in seconds.</summary>
    public float Life { get; set; }

    public float MaxLife { get; init; }

    public bool IsExpired => Life <= 0f;

    /// <summary>Progress from 0 (just struck) to 1 (finished).</summary>
    public float Progress => MaxLife <= 0f ? 1f : MathHelper.Clamp(1f - Life / MaxLife, 0f, 1f);

    /// <summary>Fade factor from 1 (just struck) to 0 (gone).</summary>
    public float Alpha => MaxLife <= 0f ? 0f : MathHelper.Clamp(Life / MaxLife, 0f, 1f);

    public void Update(float deltaTime) => Life -= deltaTime;
}

/// <summary>
/// A fragment blasted off a ship's hull when an unshielded hit lands.
/// </summary>
public class HullDebris
{
    public Vector2 Position        { get; set; }
    public Vector2 Velocity        { get; set; }
    public float   Rotation        { get; set; }
    public float   AngularVelocity { get; set; }
    public float   Size            { get; set; }
    public Color   Color           { get; init; }

    public float Life    { get; set; }
    public float MaxLife { get; init; }

    public bool IsExpired => Life <= 0f;

    public float Alpha => MaxLife <= 0f ? 0f : MathHelper.Clamp(Life / MaxLife, 0f, 1f);

    public void Update(float deltaTime)
    {
        Life     -= deltaTime;
        Position += Velocity * deltaTime;
        Rotation += AngularVelocity * deltaTime;

        // Slight drag so debris trails off rather than flying forever.
        Velocity *= 1f - MathHelper.Clamp(1.1f * deltaTime, 0f, 1f);
    }
}
