using System.Linq;
using Strange_Universe.Game.Components;
using Strange_Universe.Game.Entities;
using Xunit;

namespace Strange_Universe.Tests;

/// <summary>NPCs start with the loadout and durability of their ship type, and ships track who is hostile to them.</summary>
public class NpcLoadoutTests
{
    [Fact]
    public void Npc_InheritsWeaponsFromItsShipType()
    {
        var npc = new Nonplayer("n1");

        Assert.Equal(npc.ShipType.DefaultEquipment, npc.Equipment.Select(e => e.EquipmentName));
        Assert.Contains(npc.Equipment, e => e is Weapon { IsPrimary: true } && e.EquipmentName == "Light Laser");
        Assert.True(npc.WeaponRange > 0f);
        Assert.True(npc.ProjectileSpeed > 0f);
    }

    [Fact]
    public void Npc_StartsWithTheShipTypesHullShieldAndFuel()
    {
        var npc = new Nonplayer("n1");

        Assert.Equal(npc.ShipType.MaxHull, npc.CurrentHullStrength);
        Assert.Equal(npc.ShipType.MaxShield, npc.CurrentShieldStrength);
        Assert.Equal(npc.ShipType.MaxFuel, npc.CurrentFuelLevel);
    }

    [Fact]
    public void Npcs_DoNotShareWeaponInstances()
    {
        var a = new Nonplayer("a");
        var b = new Nonplayer("b");

        Assert.NotSame(a.Equipment[0], b.Equipment[0]);
        Assert.NotSame(a.Equipment[0], EquipmentCatalog.All.Single(e => e.EquipmentName == "Light Laser")); // nor the shared preset
    }

    [Fact]
    public void Equipment_Create_GivesSeparateCopies()
    {
        var one = Equipment.Create("Basic Shield Generator");
        var two = Equipment.Create("Basic Shield Generator");

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
