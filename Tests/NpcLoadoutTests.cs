using System.Linq;
using Strange_Universe.Game.Components;
using Strange_Universe.Game.Components.EquipmentComponents;
using Strange_Universe.Game.Components.ShipComponents;
using Strange_Universe.Game.Entities;
using Strange_Universe.Tests.Fakes;
using Xunit;

namespace Strange_Universe.Tests;

/// <summary>NPCs start with the loadout and durability of their ship type, and ships track who is hostile to them.</summary>
public class NpcLoadoutTests
{
    [Fact]
    public void Npc_InheritsWeaponsFromItsShipType()
    {
        var npc = new Nonplayer("n1");

        Assert.Equal(npc.ShipType.DefaultLoadout, npc.Equipment); // the ship holds the loadout's own objects
        Assert.Equal(new Shuttle().DefaultLoadout.Select(e => e.EquipmentName), npc.Equipment.Select(e => e.EquipmentName));

        // It can shoot exactly when its loadout holds a primary weapon.
        bool hasPrimaryWeapon = npc.Equipment.OfType<WeaponComponent>().Any(w => w.IsPrimary);
        Assert.Equal(hasPrimaryWeapon, npc.WeaponRange > 0f);
        Assert.Equal(hasPrimaryWeapon, npc.ProjectileSpeed > 0f);
    }

    [Fact]
    public void Npc_StartsWithTheShipTypesHullShieldAndFuel()
    {
        var npc = new Nonplayer("n1");

        Assert.Equal(npc.MaxHullStrength, npc.CurrentHullStrength);
        Assert.Equal(npc.MaxShieldStrength, npc.CurrentShieldStrength);
        Assert.Equal(npc.ShipType.Durability.MaxFuel, npc.CurrentFuelLevel);
    }

    [Fact]
    public void Npcs_DoNotShareWeaponInstances()
    {
        var a = new Nonplayer("a");
        var b = new Nonplayer("b");

        foreach (var item in a.Equipment)
            Assert.DoesNotContain(b.Equipment, other => ReferenceEquals(item, other));
    }

    [Fact]
    public void Equipment_Create_GivesSeparateCopies()
    {
        var shield = EquipmentCatalog.All.OfType<ShieldComponent>().First();
        var one = EquipmentComponent.Create(shield.EquipmentName);
        var two = EquipmentComponent.Create(shield.EquipmentName);

        Assert.NotNull(one);
        Assert.NotSame(one, two);
        Assert.Equal(EquipmentType.Shield, one.EquipmentType);
    }

    [Fact]
    public void Unarmed_Ship_HasNoWeaponRange()
    {
        var npc = new Nonplayer("n");
        npc.Equipment.Clear();

        Assert.Equal(0f, npc.WeaponRange);
        Assert.Equal(0f, npc.ProjectileSpeed);
    }

    // -- hostility ----------------------------------------------------------

    [Fact]
    public void MarkHostile_FlagsOthers_ButNeverSelfOrNull()
    {
        var a = new Nonplayer("a");
        var b = new Nonplayer("b");

        a.MarkHostile(a);
        a.MarkHostile(null);
        Assert.Empty(a.Hostiles);

        a.MarkHostile(b);
        a.MarkHostile(b); // flagging twice does not duplicate
        Assert.True(a.IsHostileTo(b));
        Assert.False(b.IsHostileTo(a)); // hostility is one-way until the other side is flagged too
        Assert.Single(a.Hostiles);

        a.ForgetHostile(b);
        Assert.False(a.IsHostileTo(b));
    }
}
