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
        Assert.NotSame(a.Equipment[0], Equipment.FromName("Light Laser")); // nor the shared preset
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
        Assert.True(Equipment.FromName("Light Laser").CanFire);
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
            Assert.NotSame(first, Equipment.FromName("Light Laser"));

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
    public void FromName_StillReturnsTheSharedPreset_AndCreateDoesNot()
    {
        Assert.Same(Equipment.FromName("Heavy Laser"), Equipment.FromName("heavy laser"));
        Assert.NotSame(Equipment.FromName("Heavy Laser"), Equipment.Create("Heavy Laser"));
        Assert.Null(Equipment.Create("No Such Thing"));
    }
}
