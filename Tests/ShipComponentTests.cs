using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Strange_Universe.Game.Components;
using Strange_Universe.Game.Entities;
using Strange_Universe.Game.Entities.ShipParts;
using Xunit;

namespace Strange_Universe.Tests;

public class ShipComponentTests
{
    private static FlightStats Flight => new()
    {
        ThrustForce   = 100f,
        RotationSpeed = 2f,
        MaxSpeed      = 500f,
        SoftCapStart  = 0.75f,
        Mass          = 1f,
    };

    private static DurabilityStats Durable => new() { MaxHull = 100, MaxFuel = 10 };

    // -- ShipPhysics ---------------------------------------------------------

    [Fact]
    public void Physics_BelowSoftCap_AppliesFullThrust()
    {
        var p = new ShipPhysics(() => Flight);
        p.Transform.Rotation = 0f;                      // facing +X
        p.Body.Velocity = new Vector2(100f, 0f);         // below 0.75 * 500

        p.ApplyThrust(1f);

        Assert.Equal(200f, p.Body.Velocity.X, 3);
    }

    [Fact]
    public void Physics_AtMaxSpeed_NoForwardThrust()
    {
        var p = new ShipPhysics(() => Flight);
        p.Transform.Rotation = 0f;
        p.Body.Velocity = new Vector2(500f, 0f);

        p.ApplyThrust(1f);

        Assert.Equal(500f, p.Body.Velocity.X, 3);
        Assert.Equal(0f, p.Body.Velocity.Y, 3);
    }

    [Fact]
    public void Physics_AtMaxSpeed_LateralThrustUnaffected()
    {
        var p = new ShipPhysics(() => Flight);
        p.Transform.Rotation = MathHelper.PiOver2;       // facing +Y, perpendicular to velocity
        p.Body.Velocity = new Vector2(500f, 0f);

        p.ApplyThrust(1f);

        Assert.Equal(500f, p.Body.Velocity.X, 2);
        Assert.Equal(100f, p.Body.Velocity.Y, 2);
    }

    [Fact]
    public void Physics_AtMaxSpeed_DeceleratingThrustUnaffected()
    {
        var p = new ShipPhysics(() => Flight);
        p.Transform.Rotation = MathHelper.Pi;            // facing -X (retrograde)
        p.Body.Velocity = new Vector2(500f, 0f);

        p.ApplyThrust(1f);

        Assert.Equal(400f, p.Body.Velocity.X, 2);
    }

    [Fact]
    public void Physics_InSoftZone_ThrustIsReduced()
    {
        var p = new ShipPhysics(() => Flight);
        p.Transform.Rotation = 0f;
        p.Body.Velocity = new Vector2(437.5f, 0f);       // halfway between 375 and 500

        p.ApplyThrust(1f);

        Assert.Equal(487.5f, p.Body.Velocity.X, 2);
    }

    [Fact]
    public void Physics_Integrate_ResetsNonFiniteState()
    {
        var p = new ShipPhysics(() => Flight);
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
        var p = new ShipPhysics(() => Flight);
        Assert.True(p.RotateTowards(0.5f, 1f));          // max step = 2 rad
        Assert.Equal(0.5f, p.Transform.Rotation);
        Assert.False(p.RotateTowards(float.NaN, 1f));
    }

    // -- ShipDurability ------------------------------------------------------

    [Fact]
    public void Durability_ShieldsAbsorbFirst()
    {
        var d = Fitted(Generator(strength: 50f));
        d.Shield = 50;

        d.ApplyDamage(30);

        Assert.Equal(20, d.Shield);
        Assert.Equal(100, d.Hull);
    }

    [Fact]
    public void Durability_AnActiveShieldAbsorbsTheWholeHit_NothingSpillsToTheHull()
    {
        var d = Fitted(Generator(strength: 50f));
        d.Shield = 50;

        d.ApplyDamage(80);

        Assert.Equal(0, d.Shield);
        Assert.Equal(100, d.Hull);

        d.ApplyDamage(30); // with the shield down the hull takes it
        Assert.Equal(70, d.Hull);
    }

    [Fact]
    public void Durability_HullNeverBelowZero()
    {
        var d = new ShipDurability(() => Durable) { Hull = 10, Shield = 0 };

        d.ApplyDamage(1000);

        Assert.Equal(0, d.Hull);
        Assert.True(d.IsDestroyed);
    }

    [Fact]
    public void Durability_SetsLastDamageSource()
    {
        var d = new ShipDurability(() => Durable) { Hull = 100, Shield = 0 };
        var attacker = new Nonplayer("attacker");

        d.ApplyDamage(5, attacker);

        Assert.Same(attacker, d.LastDamageSource);
    }

    [Fact]
    public void Durability_IgnoresDamageWhenDestroyedOrNonPositive()
    {
        var attacker = new Nonplayer("a");
        var d = new ShipDurability(() => Durable) { Hull = 0, Shield = 0 };
        d.ApplyDamage(5, attacker);
        Assert.Null(d.LastDamageSource);

        var e = Fitted(Generator(strength: 50f));
        e.Hull = 10;
        e.Shield = 10;
        e.ApplyDamage(0, attacker);
        Assert.Equal(10, e.Shield);
        Assert.Null(e.LastDamageSource);
    }

    [Fact]
    public void Durability_Percentages()
    {
        var d = Fitted(Generator(strength: 50f));
        d.Hull = 50;
        d.Shield = 25;
        Assert.Equal(50f, d.HullPercentage);
        Assert.Equal(50f, d.ShieldPercentage);
        Assert.Equal(0f, d.FuelPercentage);
    }

    // -- Equipment adds to the ship's base strength ---------------------------

    [Fact]
    public void MaxShieldAndHull_AreTheShipTypesValuesPlusTheEquipments()
    {
        var gear = new List<Equipment>();
        var d = new ShipDurability(() => Durable, () => gear); // base 100 hull, 50 shield
        Assert.Equal(100, d.MaxHull);
        Assert.Equal(0, d.MaxShield); // a ship type gives no shield: only equipment does

        gear.Add(new Shield { ShieldStrength = 500f });
        gear.Add(new Shield { ShieldStrength = 200f });
        gear.Add(new HullPlating { HullStrength = 300f });
        Assert.Equal(700, d.MaxShield);
        Assert.Equal(400, d.MaxHull);

        d.Shield = 350;
        Assert.Equal(50f, d.ShieldPercentage);
    }

    [Fact]
    public void AShip_StartsFullAtTheTypesValuesPlusItsEquipment()
    {
        var shuttle = new Shuttle();
        var player = new Universe("A", "a-seed").Player;
        var npc = new Nonplayer("npc");

        foreach (var ship in new Ship[] { player, npc })
        {
            Assert.Equal(25, ship.MaxShieldStrength); // Basic Shield Generator
            Assert.Equal(shuttle.Durability.MaxHull + 25, ship.MaxHullStrength);     // Light Hull Plating
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
        return new ShipDurability(() => Durable, () => gear) { Hull = 100 };
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
    public void AHit_OnlyDamagesTheFirstActiveGenerator_ButRestartsEveryGeneratorsDelay()
    {
        var first  = Generator(strength: 10f, rate: 5f, delay: 3f);
        var second = Generator(strength: 10f, rate: 5f, delay: 6f);
        var d = Fitted(first, second);
        d.Shield = 15; // the first is full, the second is at 5

        d.ApplyDamage(4);

        Assert.Equal(6f, first.Strength);
        Assert.Equal(5f, second.Strength); // untouched
        Assert.Equal(3f, first.RechargeDelayRemaining);
        Assert.Equal(6f, second.RechargeDelayRemaining); // but its recharge was put off too

        Run(d, 2f);
        Assert.Equal(5f, second.Strength); // still waiting
        Assert.Equal(6f, first.Strength);
    }

    [Fact]
    public void AGeneratorThatTakesAHit_LosesItAll_AndTheNextOneIsNotTouched()
    {
        var first  = Generator(strength: 10f);
        var second = Generator(strength: 10f);
        var d = Fitted(first, second);
        d.Shield = 20;

        d.ApplyDamage(15); // more than the first holds: the rest is lost

        Assert.Equal(0f, first.Strength);
        Assert.Equal(10f, second.Strength);
        Assert.Equal(100, d.Hull);

        d.ApplyDamage(3); // the first is down, so the next one takes this hit
        Assert.Equal(7f, second.Strength);
        Assert.Equal(100, d.Hull);
    }

    [Fact]
    public void WhenEveryGeneratorIsDown_TheHullTakesTheHit()
    {
        var first  = Generator(strength: 10f);
        var second = Generator(strength: 10f);
        var d = Fitted(first, second);
        d.Shield = 20;
        d.ApplyDamage(10);
        d.ApplyDamage(10);
        Assert.True(first.IsDown && second.IsDown);
        Assert.Equal(100, d.Hull);

        d.ApplyDamage(12);

        Assert.Equal(88, d.Hull);
    }

    [Fact]
    public void SeveralGenerators_EachRechargeByTheirOwnRateAndDelay()
    {
        var quick = Generator(strength: 10f, rate: 10f, delay: 1f);
        var slow  = Generator(strength: 10f, rate: 2f,  delay: 4f);
        var d = Fitted(quick, slow);
        d.Shield = 20;
        d.ApplyDamage(10);
        d.ApplyDamage(10);
        Assert.True(quick.IsDown && slow.IsDown);

        Run(d, 2f); // the quick one's delay (1 s) has passed, the slow one's (4 s) has not
        Assert.InRange(quick.Strength, 9.9f, 10f);
        Assert.Equal(0f, slow.Strength);

        Run(d, 4f); // 6 s in: the slow one has recharged for 2 s at 2 per second
        Assert.InRange(slow.Strength, 3.9f, 4.1f);
        Assert.Equal(10f, quick.Strength);
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
        var bare = new ShipDurability(() => Durable) { Hull = 100, Shield = 0 };
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
    public void Ship_ShipTypeIsSavedByName_AndRestoredAsTheFullType()
    {
        var ship = new Nonplayer("n");
        string json = System.Text.Json.JsonSerializer.Serialize(ship.ShipType, new System.Text.Json.JsonSerializerOptions { Converters = { new ShipStatsConverter() } });
        Assert.Equal("{\"ShipTypeName\":\"Shuttle\"}", json);

        var restored = System.Text.Json.JsonSerializer.Deserialize<ShipStats>(json, new System.Text.Json.JsonSerializerOptions { Converters = { new ShipStatsConverter() } });
        Assert.Equal(new Shuttle().Flight.MaxSpeed, restored.Flight.MaxSpeed);
        Assert.Equal("Shuttle", restored.ShipTypeName);
    }

    // -- Ship types and default loadouts ----------------------------------------

    [Fact]
    public void AssigningAShipType_GivesTheShipItsDefaultLoadout_FullOfHullShieldAndFuel_AndItCanBeChangedAfter()
    {
        var type = new Shuttle();
        var ship = new Nonplayer("n", type);

        Assert.Same(type, ship.ShipType);
        Assert.Equal(type.DefaultLoadout, ship.Equipment);
        Assert.Equal(ship.MaxHullStrength, ship.CurrentHullStrength);
        Assert.Equal(ship.MaxShieldStrength, ship.CurrentShieldStrength);
        Assert.Equal(ship.MaxFuelLevel, ship.CurrentFuelLevel);

        ship.Equipment.Clear(); // changing the loadout does not touch the type's default
        Assert.NotEmpty(type.DefaultLoadout);

        ship.AssignShipType(new Shuttle()); // assigning a type again starts over from its default
        Assert.Equal(3, ship.Equipment.Count);
    }

    [Fact]
    public void EveryShipType_HasItsOwnLoadoutObjects()
    {
        var a = new Shuttle();
        var b = new Shuttle();

        Assert.NotSame(a, b);
        for (int i = 0; i < a.DefaultLoadout.Count; i++)
            Assert.NotSame(a.DefaultLoadout[i], b.DefaultLoadout[i]);
    }

    [Fact]
    public void EveryShipType_IsASubclassOfShipStats_MadeOfTheSameSmallParts()
    {
        foreach (ShipStats type in new ShipStats[] { new Shuttle(), new Runner() })
        {
            Assert.True(type.Flight.MaxSpeed > 0f && type.Flight.ThrustForce > 0f && type.Flight.Radius > 0f);
            Assert.True(type.Durability.MaxHull > 0 && type.Durability.MaxFuel > 0);
            Assert.False(string.IsNullOrEmpty(type.Appearance.SpriteName));
            Assert.NotEmpty(type.DefaultLoadout);
        }

        Assert.Equal("Shuttle", new Shuttle().ShipTypeName);
        Assert.Equal("Runner", new Runner().ShipTypeName);
        Assert.NotEqual(new Runner().Flight.Radius, new Shuttle().Flight.Radius); // they differ (radius is not changed by the tuning override)
        Assert.NotSame(new Shuttle().Flight, new Shuttle().Flight);                // and nothing is shared between instances
    }

    [Fact]
    public void ShipTypesAreFoundByName_NotCaseSensitive_AndAnUnknownNameIsAShuttle()
    {
        Assert.IsType<Runner>(ShipStats.FromName("runner"));
        Assert.IsType<Shuttle>(ShipStats.FromName("SHUTTLE"));
        Assert.IsType<Shuttle>(ShipStats.FromName("No Such Ship"));
        Assert.IsType<Shuttle>(ShipStats.FromName(null));
    }

    [Fact]
    public void AssignShipType_UsesTheTypesOwnFlightAndDurabilityNumbers()
    {
        var runner = new Nonplayer("r", new Runner());

        Assert.Equal(new Runner().Flight.MaxSpeed, runner.MaxSpeed);
        Assert.Equal(new Runner().Flight.Radius, runner.Radius);
        Assert.Equal(new Runner().Durability.MaxFuel, runner.MaxFuelLevel);
    }

    [Fact]
    public void AssignShipType_RejectsNull()
    {
        Assert.Throws<System.ArgumentNullException>(() => new Nonplayer("n").AssignShipType(null));
    }
}
