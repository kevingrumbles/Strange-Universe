using System;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Strange_Universe.Game.Components;
using Strange_Universe.Game.Entities;
using Xunit;

namespace Strange_Universe.Tests;

/// <summary>Every ship holds its own copy of its equipment, so weapon cooldowns are never shared.</summary>
public class EquipmentInstanceTests
{
    private static string TempPath() => Path.Combine(Path.GetTempPath(), $"su-equip-{Guid.NewGuid():N}.json");

    [Fact]
    public void ANewPlayersStartingWeapon_IsItsOwnCopy()
    {
        var a = new Universe("A", "a-seed").Player;
        var b = new Universe("B", "b-seed").Player;

        Assert.Equal("Light Laser", a.Equipment.Single().EquipmentName);
        Assert.NotSame(a.Equipment[0], b.Equipment[0]);
        Assert.NotSame(a.Equipment[0], Preset("Light Laser")); // nor the preset kept in the list
    }

    [Fact]
    public void FiringOnePlayersWeapon_DoesNotPutAnothersOnCooldown()
    {
        var a = new Universe("A", "a-seed").Player;
        var b = new Universe("B", "b-seed").Player;
        Assert.True(a.Equipment[0].CanFire && b.Equipment[0].CanFire);

        // A fired shot starts the cooldown on that weapon only.
        a.Equipment[0].TryFire(a);

        Assert.False(a.Equipment[0].CanFire);
        Assert.True(b.Equipment[0].CanFire);
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

            var first = loaded[0].Player.Equipment.Single();
            var second = loaded[1].Player.Equipment.Single();
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
            universe.Player.Equipment.Add(new Equipment { EquipmentName = "Retired Gizmo", EquipmentType = EquipmentType.Utility });
            saves.Save(universe);

            var loaded = new FileSaveService(path).LoadAll().Single();
            var gizmo = loaded.Player.Equipment.Single(e => e.EquipmentName == "Retired Gizmo");
            Assert.Equal(EquipmentType.Unkown, gizmo.EquipmentType); // inert, but it keeps its name

            new FileSaveService(path).Save(loaded);
            Assert.Contains("Retired Gizmo", File.ReadAllText(path));
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void Create_AlwaysReturnsANewObject_ForTheSameName()
    {
        var first = Equipment.Create("Heavy Laser");
        var second = Equipment.Create("heavy laser"); // names are not case sensitive

        Assert.NotSame(first, second);
        Assert.NotSame(first, Preset("Heavy Laser"));
        Assert.Equal(first.Damage, second.Damage);
        Assert.Equal(first.EquipmentName, second.EquipmentName);
    }

    [Fact]
    public void Create_GivesAnInertPlaceholderForAnUnknownName_NeverNull()
    {
        var unknown = Equipment.Create("No Such Thing");

        Assert.NotNull(unknown);
        Assert.Equal("No Such Thing", unknown.EquipmentName);
        Assert.Equal(EquipmentType.Unkown, unknown.EquipmentType);
        Assert.False(unknown.PrimaryWeapon);
        Assert.NotSame(unknown, Equipment.Create("No Such Thing"));
    }

    /// <summary>The preset instance kept in the list. Ships must never hold it.</summary>
    private static Equipment Preset(string name) => Equipment.Presets.Single(e => e.EquipmentName == name);
}
