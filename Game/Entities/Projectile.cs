using Microsoft.Xna.Framework;
using Strange_Universe.Game.Components;

namespace Strange_Universe.Game.Entities;

/// <summary>
/// A fired projectile that moves independently and despawns after its lifetime expires.
/// </summary>
public class Projectile
{
    public Ship    Owner    { get; init; }
    public Vector2 Position { get; set; }
    public Vector2 Velocity { get; set; }
    public float   Rotation { get; set; }
    public float   Radius   { get; set; } = 4f;

    /// <summary>Rendering style set by the weapon that fired this projectile.</summary>
    public ProjectileVisual Visual   { get; set; } = ProjectileVisual.Default;

    /// <summary>Remaining lifetime in seconds. Projectile is removed when this reaches 0.</summary>
    public float Lifetime { get; set; }

    /// <summary>Elapsed time in seconds since this projectile was fired. Used for animation.</summary>
    public float Age { get; private set; }

    public bool IsExpired => Lifetime <= 0f;

    public void Update(float deltaTime)
    {
        Lifetime -= deltaTime;
        Age      += deltaTime;
        Position += Velocity * deltaTime;
    }
}
