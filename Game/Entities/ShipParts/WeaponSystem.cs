using System;
using System.Collections.Generic;
using Strange_Universe.Game.Components.EquipmentComponents;

namespace Strange_Universe.Game.Entities.ShipParts;

/// <summary>Additive bonuses granted by utility equipment.</summary>
public readonly record struct WeaponBonuses(float AttackSpeed, float AttackRange, float Accuracy);

/// <summary>Weapon firing, cooldowns and utility-equipment bonus aggregation.</summary>
public class WeaponSystem
{
    private readonly Func<List<EquipmentComponent>> _equipment;

    public WeaponSystem(Func<List<EquipmentComponent>> equipment)
    {
        _equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
    }

    /// <summary>Sums one bonus over all utility modules.</summary>
    public static float SumUtilityBonus(IReadOnlyList<EquipmentComponent> equipment, Func<UtilityComponent, float> bonus)
    {
        float total = 0f;
        if (equipment == null) return total;

        for (int i = 0; i < equipment.Count; i++)
        {
            if (equipment[i] is UtilityComponent utility)
                total += bonus(utility);
        }
        return total;
    }

    public WeaponBonuses CalculateBonuses()
    {
        var equipment = _equipment();
        return new WeaponBonuses(
            SumUtilityBonus(equipment, u => u.AttackSpeedBonus),
            SumUtilityBonus(equipment, u => u.AttackRangeBonus),
            SumUtilityBonus(equipment, u => u.AccuracyBonus));
    }

    /// <summary>The primary weapon with the longest range, or null when the ship is unarmed.</summary>
    private WeaponComponent LongestRangeWeapon()
    {
        var equipment = _equipment();
        WeaponComponent best = null;
        if (equipment == null) return null;
        for (int i = 0; i < equipment.Count; i++)
        {
            if (equipment[i] is WeaponComponent { IsPrimary: true } candidate && candidate.Range > (best?.Range ?? 0f))
                best = candidate;
        }
        return best;
    }

    /// <summary>Longest range among primary weapons, 0 when the ship is unarmed.</summary>
    public float MaxRange => LongestRangeWeapon()?.Range ?? 0f;

    /// <summary>Muzzle speed (relative to the shooter) of the longest-range primary weapon; 0 when unarmed.</summary>
    public float ProjectileSpeed => LongestRangeWeapon()?.ProjectileSpeed ?? 0f;

    /// <summary>Advances cooldowns on all primary weapons.</summary>
    public void UpdateCooldowns(float deltaTime)
    {
        var equipment = _equipment();
        if (equipment == null) return;

        for (int i = 0; i < equipment.Count; i++)
        {
            if (equipment[i] is WeaponComponent { IsPrimary: true } weapon)
                weapon.UpdateCooldown(deltaTime);
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
            if (equipment[i] is not WeaponComponent { IsPrimary: true } weapon) continue;

            Projectile projectile = weapon.TryFire(owner, bonuses.AttackSpeed, bonuses.AttackRange, bonuses.Accuracy);
            if (projectile != null)
                output.Add(projectile);
        }
    }
}
