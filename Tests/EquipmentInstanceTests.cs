using System;
using System.IO;
using System.Linq;
using Strange_Universe.Game.Components;
using Strange_Universe.Game.Entities;
using Xunit;

namespace Strange_Universe.Tests;

/// <summary>Every ship holds its own copy of its equipment, so weapon cooldowns are never shared.</summary>
public class EquipmentInstanceTests
{
    private static string TempPath() => Path.Combine(Path.GetTempPath(), $"su-equip-{Guid.NewGuid():N}.json");

    /// <summary>The ship's only weapon.</summary>
    private static Weapon Gun(Ship ship) => Assert.IsType<Weapon>(ship.Equipment.Single());

    /// <summary>The shared item kept in the catalog. Ships must never hold it.</summary>
    private static Weapon Preset(string name) => (Weapon)EquipmentCatalog.All.Single(e => e.EquipmentName == name);

    [Fact]
    public void ANewPlayersStartingWeapon_IsItsOwnCopy()
    {
        var a = new Universe("A", "a-seed").Player;
        var b = new Universe("B", "b-seed").Player;

        Assert.Equal("Light Laser", Gun(a).EquipmentName);
        Assert.NotSame(Gun(a), Gun(b));
        Assert.NotSame(Gun(a), Preset("Light Laser")); // nor the shared item in the catalog
    }

    [Fact]
    public void FiringOnePlayersWeapon_DoesNotPutAnothersOnCooldown()
    {
        var a = new Universe("A", "a-seed").Player;
        var b = new Universe("B", "b-seed").Player;
        Assert.True(Gun(a).CanFire && Gun(b).CanFire);

        // A fired shot starts the cooldown on that weapon only.
        Gun(a).TryFire(a);

        Assert.False(Gun(a).CanFire);
        Assert.True(Gun(b).CanFire);
        Assert.True(Preset("Light Laser").CanFire);
    }

    [Fact]
    public void LoadingASave_GivesTheShipItsOwnCopies()
    {
        string path = TempPath();
        try
        {
            var saves = new FileSaveService(path);
            saves.CreateUniverse(saves.LoadAll(), "One");
            saves.CreateUniverse(saves.LoadAll(), "Two");
            var loaded = new FileSaveService(path).LoadAll();

            var first = Gun(loaded[0].Player);
            var second = Gun(loaded[1].Player);
            Assert.Equal("Light Laser", first.EquipmentName);
            Assert.Equal(15, first.Damage); // the full stats are restored from the name
            Assert.NotSame(first, second);
            Assert.NotSame(first, Preset("Light Laser"));

            first.TryFire(loaded[0].Player);
            Assert.True(second.CanFire);
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
            universe.Player.Equipment.Add(new UtilityModule { EquipmentName = "Retired Gizmo" });
            saves.Save(universe);

            var loaded = new FileSaveService(path).LoadAll().Single();
            var gizmo = loaded.Player.Equipment.Single(e => e.EquipmentName == "Retired Gizmo");
            Assert.IsType<UnknownEquipment>(gizmo); // inert, but it keeps its name
            Assert.Equal(EquipmentType.Unkown, gizmo.EquipmentType);

            new FileSaveService(path).Save(loaded);
            Assert.Contains("Retired Gizmo", File.ReadAllText(path));
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void Create_AlwaysReturnsANewObject_ForTheSameName()
    {
        var first = Assert.IsType<Weapon>(Equipment.Create("Heavy Laser"));
        var second = Assert.IsType<Weapon>(Equipment.Create("heavy laser")); // names are not case sensitive

        Assert.NotSame(first, second);
        Assert.NotSame(first, Preset("Heavy Laser"));
        Assert.Equal(first.Damage, second.Damage);
        Assert.Equal(first.EquipmentName, second.EquipmentName);
    }

    [Fact]
    public void Create_GivesAnInertPlaceholderForAnUnknownName_NeverNull()
    {
        var unknown = Equipment.Create("No Such Thing");

        Assert.IsType<UnknownEquipment>(unknown);
        Assert.Equal("No Such Thing", unknown.EquipmentName);
        Assert.Equal(EquipmentType.Unkown, unknown.EquipmentType);
        Assert.NotSame(unknown, Equipment.Create("No Such Thing"));
    }

    [Fact]
    public void ACreatedWeapon_StartsWithAFreshCooldown()
    {
        // A copy never inherits runtime state. Clone a weapon that is cooling down and check the copy is ready.
        var original = Assert.IsType<Weapon>(Equipment.Create("Light Laser"));
        original.TryFire(new Nonplayer("shooter"));
        Assert.False(original.CanFire);

        var copy = original.Clone();

        Assert.True(((Weapon)copy).CanFire);
        Assert.False(original.CanFire); // and the original is unaffected
    }
}
