using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Strange_Universe.Game.Components;
using Strange_Universe.Game.Entities;
using Strange_Universe.Game.Entities.ShipParts;
using Xunit;

namespace Strange_Universe.Tests;

public class ShipComponentTests
{
    private static ShipStats Stats => new()
    {
        ShipTypeName = "Test",
        ThrustForce  = 100f,
        RotationSpeed = 2f,
        MaxSpeed     = 500f,
        SoftCapStart = 0.75f,
        MaxHull      = 100,
        MaxShield    = 50,
        MaxFuel      = 10,
        Mass         = 1f,
    };

    // -- ShipPhysics ---------------------------------------------------------

    [Fact]
    public void Physics_BelowSoftCap_AppliesFullThrust()
    {
        var p = new ShipPhysics(() => Stats);
        p.Transform.Rotation = 0f;                      // facing +X
        p.Body.Velocity = new Vector2(100f, 0f);         // below 0.75 * 500

        p.ApplyThrust(1f);

        Assert.Equal(200f, p.Body.Velocity.X, 3);
    }

    [Fact]
    public void Physics_AtMaxSpeed_NoForwardThrust()
    {
        var p = new ShipPhysics(() => Stats);
        p.Transform.Rotation = 0f;
        p.Body.Velocity = new Vector2(500f, 0f);

        p.ApplyThrust(1f);

        Assert.Equal(500f, p.Body.Velocity.X, 3);
        Assert.Equal(0f, p.Body.Velocity.Y, 3);
    }

    [Fact]
    public void Physics_AtMaxSpeed_LateralThrustUnaffected()
    {
        var p = new ShipPhysics(() => Stats);
        p.Transform.Rotation = MathHelper.PiOver2;       // facing +Y, perpendicular to velocity
        p.Body.Velocity = new Vector2(500f, 0f);

        p.ApplyThrust(1f);

        Assert.Equal(500f, p.Body.Velocity.X, 2);
        Assert.Equal(100f, p.Body.Velocity.Y, 2);
    }

    [Fact]
    public void Physics_AtMaxSpeed_DeceleratingThrustUnaffected()
    {
        var p = new ShipPhysics(() => Stats);
        p.Transform.Rotation = MathHelper.Pi;            // facing -X (retrograde)
        p.Body.Velocity = new Vector2(500f, 0f);

        p.ApplyThrust(1f);

        Assert.Equal(400f, p.Body.Velocity.X, 2);
    }

    [Fact]
    public void Physics_InSoftZone_ThrustIsReduced()
    {
        var p = new ShipPhysics(() => Stats);
        p.Transform.Rotation = 0f;
        p.Body.Velocity = new Vector2(437.5f, 0f);       // halfway between 375 and 500

        p.ApplyThrust(1f);

        Assert.Equal(487.5f, p.Body.Velocity.X, 2);
    }

    [Fact]
    public void Physics_Integrate_ResetsNonFiniteState()
    {
        var p = new ShipPhysics(() => Stats);
        p.Body.Velocity = new Vector2(float.NaN, 0f);
        p.Transform.Rotation = float.PositiveInfinity;

        p.Integrate(Vector2.Zero, 0.016f);

        Assert.Equal(Vector2.Zero, p.Body.Velocity);
        Assert.Equal(Vector2.Zero, p.Transform.Position);
        Assert.Equal(0f, p.Transform.Rotation);
    }

    [Fact]
    public void Physics_RotateTowards_SnapsWhenWithinStep()
    {
        var p = new ShipPhysics(() => Stats);
        Assert.True(p.RotateTowards(0.5f, 1f));          // max step = 2 rad
        Assert.Equal(0.5f, p.Transform.Rotation);
        Assert.False(p.RotateTowards(float.NaN, 1f));
    }

    // -- ShipDurability ------------------------------------------------------

    [Fact]
    public void Durability_ShieldsAbsorbFirst()
    {
        var d = new ShipDurability(() => Stats) { Hull = 100, Shield = 50 };

        d.ApplyDamage(30);

        Assert.Equal(20, d.Shield);
        Assert.Equal(100, d.Hull);
    }

    [Fact]
    public void Durability_OverflowReachesHull()
    {
        var d = new ShipDurability(() => Stats) { Hull = 100, Shield = 50 };

        d.ApplyDamage(80);

        Assert.Equal(0, d.Shield);
        Assert.Equal(70, d.Hull);
    }

    [Fact]
    public void Durability_HullNeverBelowZero()
    {
        var d = new ShipDurability(() => Stats) { Hull = 10, Shield = 0 };

        d.ApplyDamage(1000);

        Assert.Equal(0, d.Hull);
        Assert.True(d.IsDestroyed);
    }

    [Fact]
    public void Durability_SetsLastDamageSource()
    {
        var d = new ShipDurability(() => Stats) { Hull = 100, Shield = 0 };
        var attacker = new Nonplayer("attacker");

        d.ApplyDamage(5, attacker);

        Assert.Same(attacker, d.LastDamageSource);
    }

    [Fact]
    public void Durability_IgnoresDamageWhenDestroyedOrNonPositive()
    {
        var attacker = new Nonplayer("a");
        var d = new ShipDurability(() => Stats) { Hull = 0, Shield = 0 };
        d.ApplyDamage(5, attacker);
        Assert.Null(d.LastDamageSource);

        var e = new ShipDurability(() => Stats) { Hull = 10, Shield = 10 };
        e.ApplyDamage(0, attacker);
        Assert.Equal(10, e.Shield);
        Assert.Null(e.LastDamageSource);
    }

    [Fact]
    public void Durability_Percentages()
    {
        var d = new ShipDurability(() => Stats) { Hull = 50, Shield = 25, Fuel = null };
        Assert.Equal(50f, d.HullPercentage);
        Assert.Equal(50f, d.ShieldPercentage);
        Assert.Equal(0f, d.FuelPercentage);
    }

    // -- Shield recharge -----------------------------------------------------

    private static Shield Generator(float rate = 10f, float delay = 2f) => new()
    {
        EquipmentName = "Test Generator", RechargeRate = rate, RechargeDelay = delay,
    };

    private static void Run(ShipDurability d, float seconds)
    {
        for (int i = 0; i < (int)(seconds * 60f + 0.5f); i++)
            d.Update(1f / 60f);
    }

    [Fact]
    public void ShieldRecharge_WaitsForTheDelay_ThenRegainsAtTheRechargeRate()
    {
        var d = new ShipDurability(() => Stats, () => Generator(rate: 10f, delay: 2f)) { Hull = 100, Shield = 5 };
        d.ApplyDamage(5); // the shield goes down
        Assert.Equal(0, d.Shield);

        Run(d, 1.9f);
        Assert.Equal(0, d.Shield); // still inside the delay

        Run(d, 1.1f); // 3.0 s in: 1.0 s of recharging
        Assert.InRange(d.Shield!.Value, 9, 11);
    }

    [Fact]
    public void ShieldRecharge_StopsAtTheMaximum()
    {
        var d = new ShipDurability(() => Stats, () => Generator(rate: 100f, delay: 0f)) { Hull = 100, Shield = 40 };

        Run(d, 5f);

        Assert.Equal(50, d.Shield);
    }

    [Fact]
    public void ShieldRecharge_DelayRestartsEachTimeTheShieldGoesDown()
    {
        var d = new ShipDurability(() => Stats, () => Generator(rate: 10f, delay: 2f)) { Hull = 100, Shield = 5 };
        d.ApplyDamage(5);
        Run(d, 3f);
        Assert.True(d.Shield > 0);

        d.ApplyDamage(1000); // down again
        Assert.Equal(0, d.Shield);
        Assert.Equal(2f, d.RechargeDelayRemaining);
        Run(d, 1.5f);
        Assert.Equal(0, d.Shield);
    }

    [Fact]
    public void ShieldRecharge_AHitWhileTheShieldIsDown_RestartsTheDelay()
    {
        var d = new ShipDurability(() => Stats, () => Generator(rate: 10f, delay: 2f)) { Hull = 100, Shield = 5 };
        d.ApplyDamage(5);
        Run(d, 1.5f);
        Assert.Equal(0, d.Shield); // still waiting, 0.5 s to go

        d.ApplyDamage(3); // hits the hull
        Assert.Equal(2f, d.RechargeDelayRemaining);

        Run(d, 1.5f);
        Assert.Equal(0, d.Shield); // would have recharged by now without the restart
        Run(d, 1f);
        Assert.True(d.Shield > 0);
    }

    [Fact]
    public void ShieldRecharge_DoesNotHappenWithoutAGenerator_OrOnADestroyedShip()
    {
        var bare = new ShipDurability(() => Stats) { Hull = 100, Shield = 0 };
        Run(bare, 10f);
        Assert.Equal(0, bare.Shield);

        var dead = new ShipDurability(() => Stats, () => Generator(delay: 0f)) { Hull = 0, Shield = 0 };
        Run(dead, 10f);
        Assert.Equal(0, dead.Shield);
    }

    [Fact]
    public void ShieldRecharge_AShipWithAGenerator_RechargesInTheGame()
    {
        var npc = new Nonplayer("npc");
        npc.Equipment.Add(Equipment.Create("Military Shield Generator")); // 80/s after a 2 s delay
        npc.CurrentShieldStrength = 1;
        npc.CurrentHullStrength = npc.MaxHullStrength;
        npc.ApplyDamage(1);
        Assert.Equal(0, npc.CurrentShieldStrength);

        for (int i = 0; i < 60 * 3; i++)
            npc.Update(1f / 60f);

        Assert.Equal(npc.MaxShieldStrength, npc.CurrentShieldStrength); // 15 points: full within a second of the delay
    }

    // -- WeaponSystem --------------------------------------------------------

    private static UtilityModule Utility(float? fireRate, float? range, float? accuracy) => new()
    {
        EquipmentName = "util",
        AttackSpeedBonus = fireRate ?? 0f,
        AttackRangeBonus = range ?? 0f,
        AccuracyBonus = accuracy ?? 0f,
    };

    private static Weapon Gun(float fireRate) => new(EquipmentType.FixedProjectile)
    {
        EquipmentName = "gun",
        FireRate = fireRate,
        Range = 1000f,
        Accuracy = 1f,
        Damage = 5,
    };

    [Fact]
    public void Weapons_BonusAggregation_SumsUtilityOnly()
    {
        var gear = new List<Equipment>
        {
            Utility(0.5f, 100f, 0.1f),
            Utility(0.25f, null, 0.2f),
            Gun(10f),                                   // not utility: ignored
        };
        var w = new WeaponSystem(() => gear);

        var b = w.CalculateBonuses();

        Assert.Equal(0.75f, b.AttackSpeed, 4);
        Assert.Equal(100f, b.AttackRange, 4);
        Assert.Equal(0.3f, b.Accuracy, 4);
    }

    [Fact]
    public void Weapons_Cooldown_BlocksUntilElapsed()
    {
        var gun = Gun(2f);                              // 0.5 s cooldown
        var gear = new List<Equipment> { gun };
        var w = new WeaponSystem(() => gear);
        var owner = new Nonplayer("owner");
        var output = new List<Projectile>();

        w.Fire(owner, output);
        Assert.Single(output);
        Assert.False(gun.CanFire);

        w.Fire(owner, output);
        Assert.Single(output);                          // still cooling down

        w.UpdateCooldowns(0.3f);
        Assert.False(gun.CanFire);

        w.UpdateCooldowns(0.3f);
        Assert.True(gun.CanFire);

        w.Fire(owner, output);
        Assert.Equal(2, output.Count);
    }

    [Fact]
    public void Weapons_UtilityFireRateBonus_ShortensCooldown()
    {
        var gun = Gun(1f);
        var gear = new List<Equipment> { gun, Utility(1f, null, null) }; // effective 2/s
        var w = new WeaponSystem(() => gear);
        var output = new List<Projectile>();

        w.Fire(new Nonplayer("o"), output);
        w.UpdateCooldowns(0.51f);

        Assert.True(gun.CanFire);
    }

    // -- Ship facade --------------------------------------------------------

    [Fact]
    public void Ship_ShipTypeSetter_ResolvesPresetByName()
    {
        var ship = new Nonplayer();
        ship.ShipType = new ShipStats { ShipTypeName = "shuttle" };

        Assert.Equal(ShipStats.FromName("Shuttle").MaxSpeed, ship.ShipType.MaxSpeed);
        Assert.Equal("Shuttle", ship.ShipType.ShipTypeName);
    }
}
