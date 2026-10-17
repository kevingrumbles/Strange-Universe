using System;
using Microsoft.Xna.Framework;
using Strange_Universe.Game.Entities;
using Strange_Universe.Game.Entities.ShipParts;

namespace Strange_Universe.Game.Components.EquipmentComponents;

/// <summary>
/// A weapon. Its <see cref="Equipment.EquipmentType"/> says how it works (fixed or turret, projectile, beam or missile).
/// Projectile and beam weapons are the primary weapons a ship fires; missiles are not fired yet.
/// </summary>
public sealed class WeaponComponent : EquipmentComponent
{
    private float _cooldown;

    public WeaponComponent(EquipmentType kind) : base(RequireWeaponKind(kind))
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

    /// <summary>Muzzle speed of the projectile, relative to the ship that fires it. The ship's own velocity is added to it.</summary>
    public float ProjectileSpeed { get; set; } = 1000f;

    public float Range { get; set; }

    /// <summary>Shots per second.</summary>
    public float FireRate { get; set; } = 1f;

    /// <summary>
    /// 1 = every shot goes straight. Each 0.01 short of 1 widens the firing arc by 1 degree on each side, so 0.85 is a
    /// 15 degree arc, positive and negative (see <see cref="SpreadAngle"/>).
    /// </summary>
    public float Accuracy { get; set; }

    /// <summary>Degrees of arc, on each side, for every whole 1.0 of accuracy a weapon lacks (0.85 accuracy: 0.15 x 100 = 15 degrees).</summary>
    public const float ArcDegreesAtZeroAccuracy = 100f;

    /// <summary>
    /// The angle, in radians, a shot goes off the straight line. <paramref name="accuracyRoll"/> is the shot's random value
    /// from 0 to 1: 0 puts the shot as far off as the weapon's arc allows (<c>(1 - accuracy) x 100</c> degrees), 1 puts it
    /// exactly on the line, and values between are scaled linearly. <paramref name="positive"/> picks the side: a positive or a negative angle.
    /// </summary>
    public static float SpreadAngle(float accuracy, float accuracyRoll, bool positive)
    {
        float arcDegrees = (1f - Math.Clamp(accuracy, 0f, 1f)) * ArcDegreesAtZeroAccuracy;
        float offDegrees = (1f - Math.Clamp(accuracyRoll, 0f, 1f)) * arcDegrees;
        float radians = offDegrees * (MathF.PI / 180f);
        return positive ? radians : -radians;
    }

    /// <summary>How far in front of the ship the projectile appears.</summary>
    public float FiringOffset { get; set; } = 20f;

    public float ProjectileRadius { get; set; } = 4f;

    /// <summary>How this weapon's projectiles and impacts look. Each projectile it fires carries it.</summary>
    public ProjectileVisual Visual { get; set; } = new();

    /// <summary>Seconds a spent projectile stays in the world after impact. It is the length of the visual's impact burst, so the burst always plays out.</summary>
    public float ImpactLingerSeconds => Visual.BurstDuration;

    /// <summary>True for the weapons a ship fires with its fire control: projectiles and beams.</summary>
    public bool IsPrimary => EquipmentType is EquipmentType.FixedProjectile or EquipmentType.TurretProjectile
                                           or EquipmentType.FixedBeam or EquipmentType.TurretBeam;

    public bool CanFire => _cooldown <= 0f;

    /// <summary>The accuracy spread of the last shot, in radians (0 before the first shot).</summary>
    public float LastSpreadAngle { get; private set; }

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

        // Each shot rolls its own accuracy (0-100%) and its own side
        float spreadAngle = SpreadAngle(effectiveAccuracy, Random.Shared.NextSingle(), Random.Shared.Next(2) == 0);

        LastSpreadAngle = spreadAngle;

        float cos = MathF.Cos(spreadAngle);
        float sin = MathF.Sin(spreadAngle);
        Vector2 forward = owner.Forward;
        Vector2 firedDir = new Vector2(
            forward.X * cos - forward.Y * sin,
            forward.X * sin + forward.Y * cos);

        // The projectile keeps the ship's full velocity (speed and direction) and adds its own firing trajectory on top,
        // so firing sideways while moving gives a path that is the sum of the two.
        Vector2 velocity = owner.Velocity + firedDir * ProjectileSpeed;

        float speed    = velocity.Length();
        float lifetime = speed > 0f ? effectiveRange / speed : 0f;

        return new Projectile
        {
            Owner      = owner,
            Position   = spawnPos,
            Velocity   = velocity,
            Rotation   = owner.Rotation + spreadAngle, // faces the way the gun was aimed (with its spread), whatever the ship's own velocity does to the path
            Lifetime   = lifetime,
            Radius     = ProjectileRadius,
            ImpactLingerSeconds = ImpactLingerSeconds,
            Damage     = Damage,
            Visual     = Visual,
        };
    }
}
