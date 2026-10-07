using Microsoft.Xna.Framework;
using Strange_Universe.Game.Components;

namespace Strange_Universe.Game.Entities;

/// <summary>
/// A fired projectile that moves independently and despawns after its lifetime expires.
/// Gameplay state only; how it looks is decided by the renderer from <see cref="WeaponName"/>.
/// </summary>
public class Projectile
{
    public Ship    Owner    { get; init; }
    public Vector2 Position { get; set; }
    public Vector2 Velocity { get; set; }
    public float   Rotation { get; set; }
    public float   Radius   { get; set; } = 4f;

    /// <summary>Damage applied to whatever this projectile strikes.</summary>
    public int Damage { get; init; }

    /// <summary>Equipment type of the weapon that fired this projectile.</summary>
    public EquipmentType WeaponType { get; init; }

    /// <summary>Name of the weapon that fired this projectile.</summary>
    public string WeaponName { get; init; }

    /// <summary>
    /// Set once the projectile has struck something. A spent projectile stops
    /// moving and lingers for <see cref="ImpactLingerSeconds"/> before being despawned.
    /// </summary>
    public bool HasHit { get; private set; }

    /// <summary>Remaining lifetime in seconds. Projectile is removed when this reaches 0.</summary>
    public float Lifetime { get; set; }

    /// <summary>Elapsed time in seconds since this projectile was fired. Used for animation.</summary>
    public float Age { get; private set; }

    /// <summary>Seconds elapsed since impact. Only meaningful once <see cref="HasHit"/> is set.</summary>
    public float HitAge { get; private set; }

    /// <summary>Gameplay time a spent projectile remains in the world after impact. Set by the weapon.</summary>
    public float ImpactLingerSeconds { get; init; } = 0.18f;

    /// <summary>
    /// A projectile is removed once it runs out of range, or once it has lingered
    /// for <see cref="ImpactLingerSeconds"/> after impact.
    /// </summary>
    public bool IsExpired => HasHit
        ? HitAge >= ImpactLingerSeconds
        : Lifetime <= 0f;

    /// <summary>
    /// Marks this projectile as spent. It stops moving and can no longer register hits.
    /// </summary>
    public void MarkHit()
    {
        if (HasHit) return;

        HasHit   = true;
        HitAge   = 0f;
        Velocity = Vector2.Zero;
    }

    public void Update(float deltaTime)
    {
        if (HasHit)
        {
            // Spent projectiles hold position while the impact plays out.
            HitAge += deltaTime;
            return;
        }

        Lifetime -= deltaTime;
        Age      += deltaTime;
        Position += Velocity * deltaTime;
    }
}
