using Microsoft.Xna.Framework;
using System;

namespace StrangeUniverse.Game.Components;

/// <summary>
/// Represents a gravitational field around a celestial body.
/// Contains the size (radius) and intensity (strength) of the gravity well.
/// </summary>
public class GravityWell
{
    /// <summary>
    /// Fraction of player thrust force used as maximum gravity force.
    /// </summary>
    public const float GravityThrustRatio = 1.7f; 

    /// <summary>
    /// Center position of the gravity well in world space.
    /// </summary>
    public Vector2 Center { get; set; }

    /// <summary>
    /// Radius of influence in world units.
    /// Objects outside this radius experience no gravitational pull.
    /// </summary>
    public float Radius { get; set; }

    /// <summary>
    /// Strength/intensity of the gravitational field at the center.
    /// This is the maximum force applied, which falls off linearly to zero at the edge.
    /// </summary>
    private float _strength;
    public float Strength {
        get
        {
            return _strength * Launcher.ActiveUniverse.Player.ShipStats.ThrustForce * GravityWell.GravityThrustRatio;
        }
        private set
        {
            _strength = value;
        }
    }

    public GravityWell(Vector2 center, float radius, float strength)
    {
        Center = center;
        Radius = radius;
        Strength = strength;
    }

    /// <summary>
    /// Calculates the gravitational force vector at a given position.
    /// Maximum force at center, ramping linearly down to zero at the edge.
    /// Returns zero if position is outside the well's radius.
    /// </summary>
    public Vector2 CalculateForce(Vector2 position)
    {
        Vector2 toCenter = Center - position;
        float distanceSquared = toCenter.LengthSquared();

        // Outside the well's radius - no effect
        if (distanceSquared > Radius * Radius)
            return Vector2.Zero;

        float distance = MathF.Sqrt(distanceSquared);

        // Avoid division by zero at exact center
        if (distance < 0.1f)
            distance = 0.1f;

        Vector2 direction = toCenter / distance;

        // Linear falloff: force = strength * (1 - distance/radius)
        // Maximum force (= Strength) at center, zero at edge
        float falloff = 1f - (distance / Radius);
        float forceMagnitude = Strength * falloff;

        return direction * forceMagnitude;
    }
}
