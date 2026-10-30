using System;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;

namespace Strange_Universe.Game.Entities;

/// <summary>
/// Something in the world: where it is (position, rotation in radians, uniform scale) and how it moves (velocity, spin, mass). Stars, planets,
/// asteroids and ships are all one. A star or planet never moves its own velocity; a ship's flight rules live in <c>ShipPhysics</c>.
/// </summary>
public class WorldBody
{
    public virtual Vector2 Position { get; set; }

    /// <summary>Radians; 0 points right (+X).</summary>
    public float Rotation { get; set; }

    public float Scale { get; set; } = 1f;

    public Vector2 Velocity { get; set; }

    /// <summary>Radians per second (the slow spin of an asteroid).</summary>
    [JsonIgnore] public float AngularVelocity { get; set; }

    [JsonIgnore] public float Mass { get; set; } = 1f;

    /// <summary>Unit vector in the direction the body is facing.</summary>
    [JsonIgnore] public Vector2 Forward => new((float)Math.Cos(Rotation), (float)Math.Sin(Rotation));

    public void ApplyForce(Vector2 force, float deltaTime)
    {
        if (force != Vector2.Zero)
            Velocity += force * (deltaTime / Mass);
    }

    /// <summary>
    /// Moves and turns the body for one frame. No speed clamp is applied here: callers handle soft limiting where they apply force.
    /// </summary>
    public void Integrate(float deltaTime)
    {
        Position += Velocity * deltaTime;
        Rotation += AngularVelocity * deltaTime;
    }
}
