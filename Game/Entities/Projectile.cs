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

    /// <summary>Damage applied to whatever this projectile strikes.</summary>
    public int Damage { get; init; }

    /// <summary>Equipment type of the weapon that fired this projectile.</summary>
    public EquipmentType WeaponType { get; init; }

    /// <summary>Name of the weapon that fired this projectile.</summary>
    public string WeaponName { get; init; }

    /// <summary>
    /// Set once the projectile has struck something. A spent projectile stops
    /// moving and plays out its impact burst before being despawned.
    /// </summary>
    public bool HasHit { get; private set; }

    /// <summary>Rendering style set by the weapon that fired this projectile.</summary>
    public ProjectileVisual Visual   { get; set; } = ProjectileVisual.Default;

    /// <summary>Remaining lifetime in seconds. Projectile is removed when this reaches 0.</summary>
    public float Lifetime { get; set; }

    /// <summary>Elapsed time in seconds since this projectile was fired. Used for animation.</summary>
    public float Age { get; private set; }

    /// <summary>Seconds elapsed since impact. Only meaningful once <see cref="HasHit"/> is set.</summary>
    public float BurstAge { get; private set; }

    private float? _impactLingerSeconds;

    /// <summary>
    /// Gameplay time a spent projectile remains in the world after impact.
    /// Defaults to the visual burst duration so behaviour is unchanged.
    /// </summary>
    public float ImpactLingerSeconds
    {
        get => _impactLingerSeconds ?? Visual.BurstDuration;
        init => _impactLingerSeconds = value;
    }

    private bool _burstEmitted;

    /// <summary>
    /// Returns true exactly once, on the first call after impact. Lets the renderer
    /// emit the one-shot spark burst without tracking projectiles itself.
    /// </summary>
    public bool TryConsumeBurstEmission()
    {
        if (!HasHit || _burstEmitted)
            return false;

        _burstEmitted = true;
        return true;
    }

    /// <summary>True while the impact burst is still playing.</summary>
    public bool IsBursting => HasHit && BurstAge < Visual.BurstDuration;

    /// <summary>
    /// Burst progress from 0 (impact) to 1 (finished), used to drive the flash animation.
    /// </summary>
    public float BurstProgress => Visual.BurstDuration <= 0f
        ? 1f
        : MathHelper.Clamp(BurstAge / Visual.BurstDuration, 0f, 1f);

    /// <summary>
    /// A projectile is removed once it runs out of range, or after its impact
    /// burst has finished playing.
    /// </summary>
    public bool IsExpired => HasHit
        ? BurstAge >= ImpactLingerSeconds
        : Lifetime <= 0f;

    /// <summary>
    /// Marks this projectile as spent. It stops moving, can no longer register
    /// hits, and begins playing its impact burst.
    /// </summary>
    public void MarkHit()
    {
        if (HasHit) return;

        HasHit   = true;
        BurstAge = 0f;
        Velocity = Vector2.Zero;
    }

    public void Update(float deltaTime)
    {
        if (HasHit)
        {
            // Spent projectiles hold position while the burst plays out.
            BurstAge += deltaTime;
            return;
        }

        Lifetime -= deltaTime;
        Age      += deltaTime;
        Position += Velocity * deltaTime;
    }
}
