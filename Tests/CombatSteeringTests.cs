using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Strange_Universe.Game.Entities;
using Strange_Universe.Game.NavSystem;
using Strange_Universe.Tests.Fakes;
using Xunit;

namespace Strange_Universe.Tests;

/// <summary>Combat steering (lead, match velocity, pursue) and the harder alert situations.</summary>
public class CombatSteeringTests
{
    private static Projectile Shot(Ship owner) => new() { Owner = owner, Damage = 0, Lifetime = 1f };

    // -- lead aiming --------------------------------------------------------

    [Fact]
    public void LeadDirection_AtAStationaryTarget_PointsStraightAtIt()
    {
        var ship = TestGear.Arm(new Nonplayer("s") { Position = Vector2.Zero }, TestGear.Gun());
        var lead = Steering.LeadDirection(ship, new Vector2(1000, 0), Vector2.Zero);
        Assert.Equal(1f, lead.X, 3);
        Assert.Equal(0f, lead.Y, 3);
    }

    [Fact]
    public void LeadDirection_AtACrossingTarget_PointsAheadOfIt()
    {
        // The shot has to be faster than the target (300) for a lead to exist, so the test supplies its own gun.
        var ship = TestGear.Arm(new Nonplayer("s") { Position = Vector2.Zero }, TestGear.Gun(projectileSpeed: 1500f));
        var lead = Steering.LeadDirection(ship, new Vector2(1000, 0), new Vector2(0, 300)); // moving +Y

        Assert.True(lead.Y > 0.05f, "should aim toward where the target is going");
        // A shot fired along the lead direction meets the target: check by flying both for the flight time.
        float t = 1000f / ship.ProjectileSpeed;
        for (int i = 0; i < 3; i++) t = (new Vector2(1000, 0) + new Vector2(0, 300) * t).Length() / ship.ProjectileSpeed;
        var shotAt = lead * ship.ProjectileSpeed * t;
        var targetAt = new Vector2(1000, 0) + new Vector2(0, 300) * t;
        Assert.True(Vector2.Distance(shotAt, targetAt) < 5f, $"miss by {Vector2.Distance(shotAt, targetAt):F1}");
    }

    [Fact]
    public void LeadDirection_WhenUnarmed_IsZero()
    {
        var ship = new Nonplayer("s");
        ship.Equipment.Clear();
        Assert.Equal(Vector2.Zero, Steering.LeadDirection(ship, new Vector2(500, 0), Vector2.Zero));
    }

    [Fact]
    public void FireIfAimed_OnlyFiresWhenPointingAtTheLeadAndInRange()
    {
        using var sim = new NavSim();
        var target = new Vector2(800, 0);
        sim.Place(Vector2.Zero);

        sim.Ship.Rotation = MathF.PI; // pointing away
        Assert.False(Steering.FireIfAimed(sim.Ship, target, Vector2.Zero));

        sim.Ship.Rotation = 0f;       // pointing at it
        Assert.True(Steering.FireIfAimed(sim.Ship, target, Vector2.Zero));
        Assert.NotEmpty(sim.System.Projectiles);

        sim.System.Projectiles.Clear();
        sim.Ship.Weapons.UpdateCooldowns(10f);
        Assert.False(Steering.FireIfAimed(sim.Ship, new Vector2(sim.Ship.WeaponRange * 2f, 0), Vector2.Zero)); // out of range
    }

    // -- match velocity and pursue ------------------------------------------

    [Fact]
    public void MatchVelocity_ConvergesOnTheDesiredVelocity()
    {
        using var sim = new NavSim();
        sim.Place(Vector2.Zero, new Vector2(sim.MaxSpeed * 0.7f, 0));
        var desired = new Vector2(0, sim.MaxSpeed * 0.35f);

        bool matched = false;
        sim.Run(sim.TravelTime(sim.MaxSpeed) / 3f + 30f, () => matched, () => matched = Steering.MatchVelocity(sim.Ship, desired, NavSim.Dt));

        Assert.True(Vector2.Distance(sim.Ship.Velocity, desired) < sim.Ship.ShipType.Flight.MaxSpeed * Steering.VelocityTolerance + 1f);
    }

    [Fact]
    public void Pursue_CatchesAMovingTarget_AndSettlesAtTheStopDistance()
    {
        using var sim = new NavSim();
        var targetVelocity = new Vector2(0, sim.MaxSpeed * 0.35f);
        var targetPosition = new Vector2(5000, 0);
        sim.Place(Vector2.Zero);

        sim.Run(sim.TravelTime(5000f) + 60f, null, () =>
        {
            targetPosition += targetVelocity * NavSim.Dt;
            Steering.Pursue(sim.Ship, targetPosition, targetVelocity, stopDistance: 800f, NavSim.Dt);
        });

        float distance = Vector2.Distance(sim.Ship.Position, targetPosition);
        Assert.InRange(distance, 720f, 880f); // within 10% of the 800 it was asked to keep
        Assert.True(Vector2.Distance(sim.Ship.Velocity, targetVelocity) < 20f, "should be moving along with the target");
    }

    // -- harder alert situations --------------------------------------------

    [Fact]
    public void AggressiveShip_CatchesAndHitsAMovingAttacker()
    {
        using var sim = new NavSim();
        var attacker = sim.Universe.Player;
        attacker.Navigator.Clear();
        attacker.CurrentHullStrength = 1000;
        attacker.CurrentShieldStrength = 0;
        attacker.Equipment.Clear(); // no generator, so the shield stays down instead of recharging
        attacker.Position = new Vector2(5000, 0);
        attacker.Velocity = new Vector2(0, sim.MaxSpeed * 0.43f);
        sim.Place(Vector2.Zero);
        sim.Start(new PatrolTask(sim.Ship, new List<Vector2> { new(-3000, 0) }));
        sim.Ship.Navigator.Update(NavSim.Dt);
        int hullBefore = attacker.CurrentHullStrength.Value;

        sim.Ship.ApplyDamage(Shot(attacker));
        // The player is not flown here, so keep it moving at a steady velocity (well below the NPC's top speed).
        var attackerVelocity = new Vector2(0, sim.MaxSpeed * 0.43f);
        sim.RunSystem(sim.TravelTime(5000f) + 60f, () => attacker.CurrentHullStrength < hullBefore, () => attacker.Velocity = attackerVelocity);

        Assert.True(attacker.CurrentHullStrength < hullBefore, "should have caught up and hit it");
    }

    [Fact]
    public void ADefensiveShipFleeing_FightsAsSoonAsAHostileComesInRange()
    {
        using var sim = new NavSim();
        var attacker = sim.Universe.Player;
        attacker.Navigator.Clear();
        attacker.CurrentHullStrength = 1000;
        attacker.CurrentShieldStrength = 0;
        attacker.Equipment.Clear(); // no generator, so the shield stays down instead of recharging
        var planet = sim.System.Planets[^1];
        attacker.Position = new Vector2(0, -sim.System.SystemRadius * 0.9f);
        attacker.Velocity = Vector2.Zero;
        sim.Place(planet.Position + new Vector2(2000, 0));
        sim.Start(new DockTask(sim.Ship, planet.Position, holdDuration: 600f));
        sim.Ship.Navigator.Update(NavSim.Dt);

        sim.Ship.ApplyDamage(Shot(attacker));
        sim.RunSystem(1f);
        var alert = (AlertTask)sim.Ship.Navigator.ActiveTask;
        Assert.Equal(AlertTask.Phase.Flee, alert.CurrentPhase);

        attacker.Position = sim.Ship.Position + new Vector2(600, 0); // it catches up
        sim.RunSystem(0.1f);

        Assert.Equal(AlertTask.Phase.Engage, alert.CurrentPhase);
    }

    [Fact]
    public void AGuardDraggedFarFromItsOrbit_GoesBackToApproachingIt()
    {
        using var sim = new NavSim();
        var planet = sim.System.Planets[0];
        float orbit = planet.Radius * 2f;
        sim.Place(planet.Position + new Vector2(0, orbit));
        var guard = sim.Start(new GuardTask(sim.Ship, planet.Position, orbit));
        sim.Run(20f);
        Assert.Equal(GuardTask.Phase.Orbit, guard.CurrentPhase);

        sim.Ship.Position = planet.Position + new Vector2(9000, 0); // pulled far away
        sim.Run(0.1f);
        Assert.Equal(GuardTask.Phase.Approach, guard.CurrentPhase);

        sim.Run(sim.TravelTime(9000f) + 60f);
        float radius = Vector2.Distance(sim.Ship.Position, planet.Position);
        Assert.InRange(radius, orbit * 0.9f, orbit * 1.1f);
        Assert.Equal(GuardTask.Phase.Orbit, guard.CurrentPhase);
    }
}
