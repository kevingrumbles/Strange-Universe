using System;
using Microsoft.Xna.Framework;
using Strange_Universe.Game.Entities;

namespace Strange_Universe.Game.Components;

/// <summary>
/// A weapon. Its <see cref="Equipment.EquipmentType"/> says how it works (fixed or turret, projectile, beam or missile).
/// Projectile and beam weapons are the primary weapons a ship fires; missiles are not fired yet.
/// </summary>
public sealed class Weapon : Equipment
{
    private float _cooldown;

    public Weapon(EquipmentType kind) : base(RequireWeaponKind(kind))
    {
    }

    private static EquipmentType RequireWeaponKind(EquipmentType kind) => kind switch
    {
        EquipmentType.FixedProjectile or EquipmentType.TurretProjectile or EquipmentType.FixedBeam or
        EquipmentType.TurretBeam or EquipmentType.FixedMissile or EquipmentType.TurretMissile => kind,
        _ => throw new ArgumentException($"{kind} is not a kind of weapon.", nameof(kind)),
    };

    /// <summary>Energy used per shot.</summary>
    public float EnergyCost { get; set; }

    public int Damage { get; set; }

    /// <summary>Muzzle speed of the projectile, relative to the ship that fires it.</summary>
    public float ProjectileSpeed { get; set; } = 1000f;

    public float Range { get; set; }

    /// <summary>Shots per second.</summary>
    public float FireRate { get; set; } = 1f;

    /// <summary>1 = no spread, 0 = up to 22.5 degrees either way.</summary>
    public float Accuracy { get; set; }

    /// <summary>How far in front of the ship the projectile appears.</summary>
    public float FiringOffset { get; set; } = 20f;

    public float ProjectileRadius { get; set; } = 4f;

    /// <summary>Seconds a spent projectile stays in the world after impact (gameplay; the renderer plays its burst over this time).</summary>
    public float ImpactLingerSeconds { get; set; } = 0.18f;

    /// <summary>True for the weapons a ship fires with its fire control: projectiles and beams.</summary>
    public bool IsPrimary => EquipmentType is EquipmentType.FixedProjectile or EquipmentType.TurretProjectile
                                           or EquipmentType.FixedBeam or EquipmentType.TurretBeam;

    public bool CanFire => _cooldown <= 0f;

    /// <summary>Advances the weapon cooldown. Call every frame for weapons.</summary>
    public void UpdateCooldown(float deltaTime)
    {
        if (_cooldown > 0f)
            _cooldown -= deltaTime;
    }

    /// <summary>
    /// Attempts to fire. Returns a new <see cref="Projectile"/> if the cooldown
    /// has expired, otherwise returns null.
    /// </summary>
    /// <param name="owner">The ship firing this weapon.</param>
    /// <param name="attackSpeedBonus">Additive attacks-per-second bonus from ship utility gear.</param>
    /// <param name="attackRangeBonus">Additive range bonus from ship utility gear.</param>
    /// <param name="accuracyBonus">Additive accuracy bonus from ship utility gear.</param>
    public Projectile TryFire(Ship owner, float attackSpeedBonus = 0f, float attackRangeBonus = 0f, float accuracyBonus = 0f)
    {
        if (!CanFire)
            return null;

        float effectiveRate     = FireRate + attackSpeedBonus;
        float effectiveRange    = Range + attackRangeBonus;
        float effectiveAccuracy = Accuracy + accuracyBonus;

        // A weapon with no configured range would otherwise despawn its projectile
        // on the next frame, making it invisible.
        if (effectiveRange <= 0f)
            return null;
        _cooldown = effectiveRate > 0f ? 1f / effectiveRate : 0f;

        Vector2 spawnPos = owner.Position + owner.Forward * FiringOffset;

        // Accuracy of 1 = no spread; 0 = up to ±22.5° spread
        float accuracyClamped = Math.Clamp(effectiveAccuracy, 0f, 1f);
        float maxSpread       = (1f - accuracyClamped) * (MathF.PI / 8f); // 0 → ±22.5°
        float spreadAngle     = ((float)Random.Shared.NextDouble() * 2f - 1f) * maxSpread;

        float cos = MathF.Cos(spreadAngle);
        float sin = MathF.Sin(spreadAngle);
        Vector2 forward = owner.Forward;
        Vector2 firedDir = new Vector2(
            forward.X * cos - forward.Y * sin,
            forward.X * sin + forward.Y * cos);

        Vector2 velocity = owner.Velocity + firedDir * ProjectileSpeed;

        float speed    = velocity.Length();
        float lifetime = speed > 0f ? effectiveRange / speed : 0f;

        return new Projectile
        {
            Owner      = owner,
            Position   = spawnPos,
            Velocity   = velocity,
            Rotation   = owner.Rotation + spreadAngle,
            Lifetime   = lifetime,
            Radius     = ProjectileRadius,
            ImpactLingerSeconds = ImpactLingerSeconds,
            Damage     = Damage,
            WeaponType = EquipmentType,
            WeaponName = EquipmentName,
        };
    }
}
