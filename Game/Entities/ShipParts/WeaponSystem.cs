using System;
using System.Collections.Generic;
using Strange_Universe.Game.Components;

namespace Strange_Universe.Game.Entities.ShipParts;

/// <summary>Additive bonuses granted by utility equipment.</summary>
public readonly record struct WeaponBonuses(float AttackSpeed, float AttackRange, float Accuracy);

/// <summary>Weapon firing, cooldowns and utility-equipment bonus aggregation.</summary>
public class WeaponSystem
{
    private readonly Func<List<Equipment>> _equipment;

    public WeaponSystem(Func<List<Equipment>> equipment)
    {
        _equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
    }

    /// <summary>Sums one stat over all utility equipment.</summary>
    public static float SumUtilityBonus(IReadOnlyList<Equipment> equipment, Func<Equipment, float?> stat)
    {
        float total = 0f;
        if (equipment == null) return total;

        for (int i = 0; i < equipment.Count; i++)
        {
            var e = equipment[i];
            if (e.EquipmentType == EquipmentType.Utility)
                total += stat(e) ?? 0f;
        }
        return total;
    }

    public WeaponBonuses CalculateBonuses()
    {
        var equipment = _equipment();
        return new WeaponBonuses(
            SumUtilityBonus(equipment, e => e.FireRate),
            SumUtilityBonus(equipment, e => e.Range),
            SumUtilityBonus(equipment, e => e.Accuracy));
    }

    /// <summary>The primary weapon with the longest range, or null when the ship is unarmed.</summary>
    private Equipment LongestRangeWeapon()
    {
        var equipment = _equipment();
        Equipment best = null;
        if (equipment == null) return null;
        for (int i = 0; i < equipment.Count; i++)
        {
            var weapon = equipment[i];
            if (weapon.PrimaryWeapon && (weapon.Range ?? 0f) > (best?.Range ?? 0f))
                best = weapon;
        }
        return best;
    }

    /// <summary>Longest range among primary weapons, 0 when the ship is unarmed.</summary>
    public float MaxRange => LongestRangeWeapon()?.Range ?? 0f;

    /// <summary>Muzzle speed (relative to the shooter) of the longest-range primary weapon; 0 when unarmed.</summary>
    public float ProjectileSpeed
    {
        get
        {
            var weapon = LongestRangeWeapon();
            return weapon == null ? 0f : (weapon.Speed ?? weapon.ProjectileSpeed);
        }
    }

    /// <summary>Advances cooldowns on all primary weapons.</summary>
    public void UpdateCooldowns(float deltaTime)
    {
        var equipment = _equipment();
        if (equipment == null) return;

        for (int i = 0; i < equipment.Count; i++)
        {
            if (equipment[i].PrimaryWeapon)
                equipment[i].UpdateCooldown(deltaTime);
        }
    }

    /// <summary>Fires every ready primary weapon, adding projectiles to <paramref name="output"/>.</summary>
    public void Fire(Ship owner, List<Projectile> output)
    {
        var equipment = _equipment();
        if (equipment == null || output == null) return;

        WeaponBonuses bonuses = CalculateBonuses();
        for (int i = 0; i < equipment.Count; i++)
        {
            var weapon = equipment[i];
            if (!weapon.PrimaryWeapon) continue;

            Projectile projectile = weapon.TryFire(owner, bonuses.AttackSpeed, bonuses.AttackRange, bonuses.Accuracy);
            if (projectile != null)
                output.Add(projectile);
        }
    }
}
