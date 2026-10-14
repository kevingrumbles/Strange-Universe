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

    // -- Equipment adds to the ship's base strength ---------------------------

    [Fact]
    public void MaxShieldAndHull_AreTheShipTypesValuesPlusTheEquipments()
    {
        var gear = new List<Equipment>();
        var d = new ShipDurability(() => Stats, () => gear); // base 100 hull, 50 shield
        Assert.Equal(100, d.MaxHull);
        Assert.Equal(50, d.MaxShield);

        gear.Add(new Shield { ShieldStrength = 500f });
        gear.Add(new Shield { ShieldStrength = 200f });
        gear.Add(new HullPlating { HullStrength = 300f });
        Assert.Equal(750, d.MaxShield);
        Assert.Equal(400, d.MaxHull);

        d.Shield = 375;
        Assert.Equal(50f, d.ShieldPercentage);
    }

    [Fact]
    public void AShip_StartsFullAtTheTypesValuesPlusItsEquipment()
    {
        var shuttle = ShipStats.Shuttle;
        var player = new Universe("A", "a-seed").Player;
        var npc = new Nonplayer("npc");

        foreach (var ship in new Ship[] { player, npc })
        {
            Assert.Equal(shuttle.MaxShield + 25, ship.MaxShieldStrength); // Basic Shield Generator
            Assert.Equal(shuttle.MaxHull + 25, ship.MaxHullStrength);     // Light Hull Plating
            Assert.Equal(ship.MaxShieldStrength, ship.CurrentShieldStrength);
            Assert.Equal(ship.MaxHullStrength, ship.CurrentHullStrength);
        }
    }

    // -- Shield recharge: each generator holds and recharges its own strength ---

    private static Shield Generator(float strength = 20f, float rate = 10f, float delay = 2f) => new()
    {
        EquipmentName = "Test Generator", ShieldStrength = strength, RechargeRate = rate, RechargeDelay = delay,
    };

    private static ShipDurability Fitted(params Shield[] generators)
    {
        var gear = new List<Equipment>(generators);
        return new ShipDurability(() => Stats, () => gear) { Hull = 100 };
    }

    private static void Run(ShipDurability d, float seconds)
    {
        for (int i = 0; i < (int)(seconds * 60f + 0.5f); i++)
            d.Update(1f / 60f);
    }

    [Fact]
    public void AGenerator_WaitsForItsDelay_ThenRegainsAtItsRechargeRate()
    {
        var g = Generator(strength: 20f, rate: 10f, delay: 2f);
        var d = Fitted(g);
        d.Shield = 5;
        d.ApplyDamage(5); // the shield goes down
        Assert.Equal(0, d.Shield);
        Assert.Equal(2f, g.RechargeDelayRemaining);

        Run(d, 1.9f);
        Assert.Equal(0, d.Shield); // still inside the delay

        Run(d, 1.1f); // 3.0 s in: 1.0 s of recharging
        Assert.InRange(d.Shield!.Value, 9, 11);
    }

    [Fact]
    public void AGenerator_StopsRechargingAtItsMaximum()
    {
        var g = Generator(strength: 20f, rate: 100f, delay: 0f);
        var d = Fitted(g);
        d.Shield = 5;

        Run(d, 5f);

        Assert.Equal(20f, g.Strength);
        Assert.Equal(20, d.Shield);
    }

    [Fact]
    public void AGenerator_DelayRestartsEachTimeItGoesDown()
    {
        var g = Generator(delay: 2f);
        var d = Fitted(g);
        d.Shield = 5;
        d.ApplyDamage(5);
        Run(d, 3f);
        Assert.True(d.Shield > 0);

        d.ApplyDamage(1000); // down again
        Assert.Equal(0, d.Shield);
        Assert.Equal(2f, g.RechargeDelayRemaining);
        Run(d, 1.5f);
        Assert.Equal(0, d.Shield);
    }

    [Fact]
    public void AHitWhileTheGeneratorIsDown_RestartsItsDelay()
    {
        var g = Generator(rate: 10f, delay: 2f);
        var d = Fitted(g);
        d.Shield = 5;
        d.ApplyDamage(5);
        Run(d, 1.5f);
        Assert.Equal(0, d.Shield); // still waiting, 0.5 s to go

        d.ApplyDamage(3); // hits the hull
        Assert.Equal(2f, g.RechargeDelayRemaining);

        Run(d, 1.5f);
        Assert.Equal(0, d.Shield); // would have recharged by now without the restart
        Run(d, 1f);
        Assert.True(d.Shield > 0);
    }

    [Fact]
    public void SeveralGenerators_TakeDamageInTurn_AndEachRechargesByItsOwnRateAndDelay()
    {
        var quick = Generator(strength: 10f, rate: 10f, delay: 1f);
        var slow  = Generator(strength: 10f, rate: 2f,  delay: 4f);
        var d = Fitted(quick, slow);
        d.Shield = 20;
        Assert.Equal(20, d.MaxShield - 50); // the two generators add 20 to the ship type's 50

        d.ApplyDamage(15); // the first takes 10, the second 5
        Assert.Equal(0f, quick.Strength);
        Assert.Equal(5f, slow.Strength);
        Assert.Equal(5, d.Shield);

        d.ApplyDamage(5);  // the second goes down too
        Assert.True(quick.IsDown && slow.IsDown);

        Run(d, 2f); // the quick one's delay (1 s) has passed, the slow one's (4 s) has not
        Assert.InRange(quick.Strength, 9.9f, 10f);
        Assert.Equal(0f, slow.Strength);

        Run(d, 4f); // 6 s in: the slow one has recharged for 2 s at 2 per second
        Assert.InRange(slow.Strength, 3.9f, 4.1f);
        Assert.Equal(10f, quick.Strength);
    }

    [Fact]
    public void ADownedGenerator_OnlyRestartsItsDelay_WhenTheDamageReachesIt()
    {
        var first  = Generator(strength: 10f, delay: 3f);
        var second = Generator(strength: 10f, delay: 3f);
        var d = Fitted(first, second);
        d.Shield = 10;      // the first is full, the second is empty
        d.ApplyDamage(10);  // the first takes it all and goes down; the damage never reaches the second
        Assert.Equal(3f, first.RechargeDelayRemaining);
        Assert.Equal(0f, second.RechargeDelayRemaining);

        Run(d, 1f);         // the second, with no delay, recharges; the first is still waiting
        Assert.Equal(2f, first.RechargeDelayRemaining, 1);
        d.ApplyDamage(1);   // the downed first is passed over and hit again, so its delay restarts; the second takes the damage
        Assert.Equal(3f, first.RechargeDelayRemaining);
        Assert.Equal(0f, second.RechargeDelayRemaining);
        Assert.True(second.Strength < 10f);
    }

    [Fact]
    public void TheShieldTotal_IsWhatIsSaved_AndIsHandedBackToTheGeneratorsOnLoad()
    {
        var first  = Generator(strength: 10f);
        var second = Generator(strength: 10f);
        var d = Fitted(first, second);

        d.Shield = 14;

        Assert.Equal(14, d.Shield);
        Assert.Equal(10f, first.Strength);
        Assert.Equal(4f, second.Strength);
    }

    [Fact]
    public void ShieldRecharge_DoesNotHappenWithoutAGenerator_OrOnADestroyedShip()
    {
        var bare = new ShipDurability(() => Stats) { Hull = 100, Shield = 0 };
        Run(bare, 10f);
        Assert.Equal(0, bare.Shield);

        var dead = Fitted(Generator(delay: 0f));
        dead.Hull = 0;
        dead.Shield = 0;
        Run(dead, 10f);
        Assert.Equal(0, dead.Shield);
    }

    [Fact]
    public void ShieldRecharge_AShipWithAGenerator_RechargesInTheGame()
    {
        var npc = new Nonplayer("npc");
        npc.Equipment.Clear();
        npc.Equipment.Add(Equipment.Create("Military Shield Generator")); // 16/s after a 2 s delay
        npc.CurrentShieldStrength = 1;
        npc.CurrentHullStrength = npc.MaxHullStrength;
        npc.ApplyDamage(1);
        Assert.Equal(0, npc.CurrentShieldStrength);

        for (int i = 0; i < 60 * 3; i++)
            npc.Update(1f / 60f);

        Assert.InRange(npc.CurrentShieldStrength!.Value, 15, 17); // 3 s in, 2 s of it delay: about 1 s at 16 per second
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
