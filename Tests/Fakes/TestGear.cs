using Strange_Universe.Game.Components.EquipmentComponents;
using Strange_Universe.Game.Entities;

namespace Strange_Universe.Tests.Fakes;

/// <summary>
/// Equipment built by the tests themselves, with the numbers a test needs written out. Tests that depend on what a ship carries
/// give it this gear (<see cref="Arm"/>) instead of relying on a ship type's default loadout or the catalog's numbers, which
/// are free to change.
/// </summary>
public static class TestGear
{
    public static WeaponComponent Gun(float projectileSpeed = 1500f, float range = 1500f, float fireRate = 3f, float accuracy = 1f, int damage = 10) =>
        new(EquipmentType.FixedProjectile)
        {
            EquipmentName = "Test Gun", ProjectileSpeed = projectileSpeed, Range = range, FireRate = fireRate, Accuracy = accuracy, Damage = damage,
        };

    public static ShieldComponent Shield(float strength = 20f, float rechargeRate = 10f, float rechargeDelay = 2f) =>
        new() { EquipmentName = "Test Shield", ShieldStrength = strength, RechargeRate = rechargeRate, RechargeDelay = rechargeDelay };

    public static PlatingComponent Plating(float strength = 20f) =>
        new() { EquipmentName = "Test Plating", HullStrength = strength };

    /// <summary>Replaces everything the ship carries with <paramref name="gear"/>, and refills its hull, shield and fuel.</summary>
    public static T Arm<T>(T ship, params EquipmentComponent[] gear) where T : Ship
    {
        ship.Equipment.Clear();
        ship.Equipment.AddRange(gear);
        ship.CurrentHullStrength = ship.MaxHullStrength;
        ship.CurrentShieldStrength = ship.MaxShieldStrength;
        return ship;
    }
}
