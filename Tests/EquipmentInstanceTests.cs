using System;
using System.IO;
using System.Linq;
using Strange_Universe.Game.Components.EquipmentComponents;
using Strange_Universe.Game.Components.ShipComponents;
using Strange_Universe.Game.Entities;
using Strange_Universe.Tests.Fakes;
using Xunit;

namespace Strange_Universe.Tests;

/// <summary>Every ship holds its own copy of its equipment, so weapon cooldowns are never shared.</summary>
public class EquipmentInstanceTests
{
    private static string TempPath() => Path.Combine(Path.GetTempPath(), $"su-equip-{Guid.NewGuid():N}.json");

    private static string AnyShieldName() => EquipmentCatalog.All.OfType<ShieldComponent>().First().EquipmentName;

    private static string AnyPrimaryWeaponName() => EquipmentCatalog.All.OfType<WeaponComponent>().First(w => w.IsPrimary).EquipmentName;

    [Fact]
    public void EveryShipsEquipment_IsItsOwnCopy_WhateverTheLoadout()
    {
        var a = new Universe("A", "a-seed").Player;
        var b = new Universe("B", "b-seed").Player;

        Assert.Equal(a.Equipment.Select(e => e.EquipmentName), b.Equipment.Select(e => e.EquipmentName)); // the same loadout...
        foreach (var item in a.Equipment)
            Assert.DoesNotContain(b.Equipment, other => ReferenceEquals(item, other));                    // ...but no shared objects
    }

    [Fact]
    public void FiringOneShipsWeapon_DoesNotPutAnothersOnCooldown()
    {
        var a = TestGear.Arm(new Nonplayer("a"), TestGear.Gun());
        var b = TestGear.Arm(new Nonplayer("b"), TestGear.Gun());
        var gunA = a.Equipment.OfType<WeaponComponent>().Single();
        var gunB = b.Equipment.OfType<WeaponComponent>().Single();
        Assert.True(gunA.CanFire && gunB.CanFire);

        // A fired shot starts the cooldown on that weapon only.
        gunA.TryFire(a);

        Assert.False(gunA.CanFire);
        Assert.True(gunB.CanFire);
    }

    /// <summary>A ship carrying only <paramref name="guns"/> guns, moving at <paramref name="velocity"/> and facing +X.</summary>
    private static Nonplayer ShipWithGuns(Microsoft.Xna.Framework.Vector2 velocity, int guns = 2, float accuracy = 1f)
    {
        var ship = new Nonplayer("shooter") { Rotation = 0f, Velocity = velocity };
        return TestGear.Arm(ship, Enumerable.Range(0, guns).Select(_ => (EquipmentComponent)TestGear.Gun(accuracy: accuracy)).ToArray());
    }

    [Fact]
    public void AProjectile_KeepsTheShipsFullVelocity_PlusItsOwnFiringTrajectory()
    {
        var ship = ShipWithGuns(new Microsoft.Xna.Framework.Vector2(0, 500)); // facing +X, moving +Y
        var shots = new System.Collections.Generic.List<Projectile>();

        ship.Weapons.Fire(ship, shots);

        Assert.Equal(ship.Equipment.OfType<WeaponComponent>().Count(), shots.Count);
        float muzzle = ship.Equipment.OfType<WeaponComponent>().First().ProjectileSpeed;
        foreach (var shot in shots)
        {
            Assert.Equal(muzzle, shot.Velocity.X, 2); // its own trajectory, along the facing
            Assert.Equal(500f, shot.Velocity.Y, 2);   // plus the ship's whole velocity
        }
    }

    [Fact]
    public void AProjectile_IsFacedTheWayTheGunWasAimed_NotTheWayItTravels()
    {
        foreach (var velocity in new[] { new Microsoft.Xna.Framework.Vector2(0, 500), new Microsoft.Xna.Framework.Vector2(-300, 200), new Microsoft.Xna.Framework.Vector2(700, 0) })
        {
            var ship = ShipWithGuns(velocity);
            ship.Rotation = 0.6f;
            var shots = new System.Collections.Generic.List<Projectile>();

            ship.Weapons.Fire(ship, shots);

            Assert.All(shots, shot => Assert.Equal(0.6f, shot.Rotation, 4)); // the gun's facing, whatever the ship is doing
        }
    }

    [Fact]
    public void AProjectilesFacing_IncludesTheGunsOwnSpread_ButNotTheShipsMotion()
    {
        var ship = ShipWithGuns(new Microsoft.Xna.Framework.Vector2(0, 500), accuracy: 0.8f);
        var shots = new System.Collections.Generic.List<Projectile>();
        var guns = ship.Equipment.OfType<WeaponComponent>().ToList();

        for (int i = 0; i < 50; i++)
        {
            shots.Clear();
            foreach (var gun in guns) gun.UpdateCooldown(10f);
            ship.Weapons.Fire(ship, shots);
            for (int g = 0; g < shots.Count; g++)
                Assert.Equal(guns[g].LastSpreadAngle, shots[g].Rotation, 4); // facing = ship facing (0) + that shot's spread
        }
    }

    [Fact]
    public void Accuracy_SetsAnArcOnEachSide_AndTheRollPlacesTheShotInIt()
    {
        float Degrees(float accuracy, float roll, bool positive) => WeaponComponent.SpreadAngle(accuracy, roll, positive) * 180f / MathF.PI;

        // 0.85 accuracy is a 15 degree arc, positive and negative; a roll of 0% is as far off as the arc allows, 100% is dead on
        Assert.Equal(15f, Degrees(0.85f, 0f, positive: true), 3);
        Assert.Equal(-15f, Degrees(0.85f, 0f, positive: false), 3);
        Assert.Equal(0f, Degrees(0.85f, 1f, positive: true), 3);
        Assert.Equal(0f, Degrees(0.85f, 1f, positive: false), 3);
        Assert.Equal(7.5f, Degrees(0.85f, 0.5f, positive: true), 3);

        Assert.Equal(0f, Degrees(1f, 0f, positive: true), 3);      // perfect accuracy never strays
        Assert.Equal(5f, Degrees(0.95f, 0f, positive: true), 3);   // each 0.01 short of 1 is a degree
        Assert.Equal(15f, Degrees(0.85f, -3f, positive: true), 3); // out-of-range rolls are clamped
        Assert.Equal(0f, Degrees(0.85f, 4f, positive: true), 3);
        Assert.Equal(100f, Degrees(-1f, 0f, positive: true), 3);   // accuracy is clamped to 0-1
    }

    [Fact]
    public void EveryShot_LandsInsideItsWeaponsArc_OnBothSides()
    {
        var gun = new WeaponComponent(EquipmentType.FixedProjectile) { Accuracy = 0.85f, Range = 1000f, ProjectileSpeed = 1000f, FireRate = 1f };
        var ship = new Nonplayer("still");
        float arc = 15f * MathF.PI / 180f;
        int positive = 0, negative = 0;

        for (int i = 0; i < 2000; i++)
        {
            gun.UpdateCooldown(10f);
            gun.TryFire(ship);
            Assert.InRange(gun.LastSpreadAngle, -arc - 1e-5f, arc + 1e-5f);
            if (gun.LastSpreadAngle > 0f) positive++;
            if (gun.LastSpreadAngle < 0f) negative++;
        }

        Assert.InRange(positive, 800, 1200);
        Assert.InRange(negative, 800, 1200);
    }

    [Fact]
    public void TwoWeaponsOfTheSameKind_EachRollTheirOwnAccuracySpread()
    {
        var ship = ShipWithGuns(Microsoft.Xna.Framework.Vector2.Zero, guns: 2, accuracy: 0.5f);
        var shots = new System.Collections.Generic.List<Projectile>();
        int pairs = 0, identical = 0;

        for (int i = 0; i < 200; i++)
        {
            shots.Clear();
            ship.Weapons.UpdateCooldowns(10f); // both are ready
            ship.Weapons.Fire(ship, shots);

            Assert.Equal(2, shots.Count);
            pairs++;
            if (shots[0].Rotation == shots[1].Rotation) identical++;
        }

        Assert.True(identical <= 1, $"{identical} of {pairs} pairs had exactly the same spread: the weapons share a roll");
    }

    [Fact]
    public void LoadingASave_GivesTheShipItsOwnCopies_RestoredFromTheirNames()
    {
        string path = TempPath();
        try
        {
            var saves = new FileSaveService(path);
            var universe = new Universe("One", "one-seed");
            universe.Player.Equipment.Clear();
            universe.Player.Equipment.Add(EquipmentComponent.Create(AnyPrimaryWeaponName()));
            universe.Player.Equipment.Add(EquipmentComponent.Create(AnyPrimaryWeaponName()));
            saves.Save(universe);

            var loaded = new FileSaveService(path).LoadAll().Single().Player;

            var guns = loaded.Equipment.OfType<WeaponComponent>().ToList();
            Assert.Equal(2, guns.Count);
            var catalogCopy = (WeaponComponent)EquipmentComponent.Create(guns[0].EquipmentName);
            Assert.Equal(catalogCopy.Damage, guns[0].Damage); // the full stats are restored from the name
            Assert.NotSame(guns[0], guns[1]);

            guns[0].TryFire(loaded);
            Assert.True(guns[1].CanFire);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void ADamagedShield_IsRestoredByALoad_IntoTheGenerator()
    {
        string path = TempPath();
        try
        {
            var saves = new FileSaveService(path);
            var universe = new Universe("Hit", "hit-seed");
            TestGear.Arm(universe.Player, EquipmentComponent.Create(AnyShieldName()));
            universe.Player.ApplyDamage(1); // eats into the shield generator
            int shield = universe.Player.CurrentShieldStrength!.Value;
            Assert.True(shield < universe.Player.MaxShieldStrength);
            saves.Save(universe);

            var loaded = new FileSaveService(path).LoadAll().Single().Player;

            Assert.Equal(shield, loaded.CurrentShieldStrength);
            Assert.Equal(shield, (int)loaded.Equipment.OfType<ShieldComponent>().Single().Strength);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void AShipType_IsSavedByName_AndLoadsWithTheSavedEquipmentInsteadOfTheDefaultLoadout()
    {
        string path = TempPath();
        try
        {
            var universe = new Universe("Fast", "fast-seed");
            universe.Player.AssignShipType(new Runner());
            string[] defaults = universe.Player.Equipment.Select(e => e.EquipmentName).ToArray();
            universe.Player.Equipment.RemoveAt(0);                     // the loadout was changed after it was assigned
            universe.Player.Equipment.Add(new UtilityComponent { EquipmentName = "Added Later" });
            string[] changed = universe.Player.Equipment.Select(e => e.EquipmentName).ToArray();
            Assert.NotEqual(defaults, changed);
            new FileSaveService(path).Save(universe);

            var loaded = new FileSaveService(path).LoadAll().Single().Player;

            Assert.Equal("Runner", loaded.ShipType.ShipTypeName);
            Assert.Equal(new Runner().Flight.MaxSpeed, loaded.ShipType.Flight.MaxSpeed);   // the full type, not the Shuttle
            Assert.Equal(changed, loaded.Equipment.Select(e => e.EquipmentName));          // the saved equipment, not the default one
            Assert.Empty(loaded.ShipType.DefaultLoadout);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void AnItemThatIsNoLongerKnown_SurvivesALoadAndSave_ByName()
    {
        string path = TempPath();
        try
        {
            var saves = new FileSaveService(path);
            var universe = new Universe("Old", "old-seed");
            universe.Player.Equipment.Add(new UtilityComponent { EquipmentName = "Retired Gizmo" });
            saves.Save(universe);

            var loaded = new FileSaveService(path).LoadAll().Single();
            var gizmo = loaded.Player.Equipment.Single(e => e.EquipmentName == "Retired Gizmo");
            Assert.IsType<UnknownComponent>(gizmo); // inert, but it keeps its name
            Assert.Equal(EquipmentType.Unkown, gizmo.EquipmentType);

            new FileSaveService(path).Save(loaded);
            Assert.Contains("Retired Gizmo", File.ReadAllText(path));
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void Create_AlwaysReturnsANewObject_ForTheSameName()
    {
        string name = AnyPrimaryWeaponName();
        var first = Assert.IsType<WeaponComponent>(EquipmentComponent.Create(name));
        var second = Assert.IsType<WeaponComponent>(EquipmentComponent.Create(name.ToLowerInvariant())); // names are not case sensitive

        Assert.NotSame(first, second);
        Assert.Equal(first.Damage, second.Damage);
        Assert.Equal(first.EquipmentName, second.EquipmentName);
    }

    [Fact]
    public void Create_GivesAnInertPlaceholderForAnUnknownName_NeverNull()
    {
        var unknown = EquipmentComponent.Create("No Such Thing");

        Assert.IsType<UnknownComponent>(unknown);
        Assert.Equal("No Such Thing", unknown.EquipmentName);
        Assert.Equal(EquipmentType.Unkown, unknown.EquipmentType);
        Assert.NotSame(unknown, EquipmentComponent.Create("No Such Thing"));
    }

    [Fact]
    public void ACreatedWeapon_StartsWithAFreshCooldown_EvenAfterAnotherOneFired()
    {
        string name = AnyPrimaryWeaponName();
        var first = Assert.IsType<WeaponComponent>(EquipmentComponent.Create(name));
        first.TryFire(new Nonplayer("shooter"));
        Assert.False(first.CanFire);

        var second = Assert.IsType<WeaponComponent>(EquipmentComponent.Create(name));
        Assert.True(second.CanFire);
        Assert.False(first.CanFire); // and the first is unaffected
    }

    [Fact]
    public void TheCatalog_BuildsEverythingFresh_AndEveryNameIsUnique()
    {
        var first = EquipmentCatalog.All;
        var second = EquipmentCatalog.All;

        Assert.NotEmpty(first);
        Assert.Equal(first.Count, first.Select(e => e.EquipmentName).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        for (int i = 0; i < first.Count; i++)
            Assert.NotSame(first[i], second[i]); // nothing is shared, not even between two reads of the catalog
    }
}
