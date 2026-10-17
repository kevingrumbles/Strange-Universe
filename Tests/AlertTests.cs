using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Strange_Universe.Game.Components;
using Strange_Universe.Game.Entities;
using Strange_Universe.Game.Entities.ShipParts;
using Strange_Universe.Game.NavSystem;
using Strange_Universe.Tests.Fakes;
using Xunit;

namespace Strange_Universe.Tests;

/// <summary>
/// An NPC that is shot at goes on alert: the shooter is flagged hostile, the task it was carrying out is stored,
/// and that task decides how it responds (aggressive: hunt and attack; defensive: fight only if close, otherwise leave).
/// </summary>
public class AlertTests
{
    /// <summary>A projectile that does no damage, so a test can trigger the alert without hurting the target.</summary>
    private static Projectile Shot(Ship owner, int damage = 0) => new() { Owner = owner, Damage = damage, Lifetime = 1f };

    private static (NavSim sim, Player attacker) Setup()
    {
        var sim = new NavSim();
        var attacker = sim.Universe.Player;
        attacker.Navigator.Clear(); // its opening spawn task would move it to the entry point
        attacker.CurrentHullStrength = 1000; // survives return fire
        attacker.CurrentShieldStrength = 0;
        attacker.Equipment.Clear(); // no generator, so the shield stays down instead of recharging
        return (sim, attacker);
    }

    private static AlertTask Alert(Ship ship) => ship.Navigator.ActiveTask as AlertTask;

    private static PatrolTask StartPatrol(NavSim sim)
    {
        var patrol = sim.Start(new PatrolTask(sim.Ship, new List<Vector2> { new(4000, 0), new(4000, 4000) }));
        sim.Ship.Navigator.Update(NavSim.Dt); // starts it
        return patrol;
    }

    // -- minimap color --------------------------------------------------------

    [Fact]
    public void OnTheMinimap_DefensiveShipsAreBlue_AggressiveYellow_AndHostileOnesRed()
    {
        var (sim, player) = Setup();
        using var _ = sim;
        var npc = sim.Ship;
        npc.Navigator.Clear();
        Assert.Equal(Strange_Universe.Game.Systems.SpriteRenderer.DefensiveShipColor, Strange_Universe.Game.Systems.SpriteRenderer.NpcMinimapColor(npc, player)); // idle

        StartPatrol(sim); // patrolling is aggressive
        Assert.Equal(Strange_Universe.Game.Systems.SpriteRenderer.AggressiveShipColor, Strange_Universe.Game.Systems.SpriteRenderer.NpcMinimapColor(npc, player));

        npc.ApplyDamage(Shot(player)); // shot by the player: it is hostile to the player now
        Assert.Equal(Strange_Universe.Game.Systems.SpriteRenderer.HostileShipColor, Strange_Universe.Game.Systems.SpriteRenderer.NpcMinimapColor(npc, player));

        npc.ForgetHostile(player); // alerting and no longer hostile: the stored aggressive task decides
        Assert.Equal(Strange_Universe.Game.Systems.SpriteRenderer.AggressiveShipColor, Strange_Universe.Game.Systems.SpriteRenderer.NpcMinimapColor(npc, player));
    }

    // -- trigger and bookkeeping --------------------------------------------

    [Fact]
    public void BeingShot_FlagsTheShooterHostile_AndStoresThePreviousTask()
    {
        var (sim, attacker) = Setup();
        using var _ = sim;
        var patrol = StartPatrol(sim);

        sim.Ship.ApplyDamage(Shot(attacker));

        Assert.True(sim.Ship.IsHostileTo(attacker));
        Assert.True(sim.Ship.Navigator.IsAlerting);
        Assert.Same(patrol, Alert(sim.Ship).Previous);
        Assert.Equal(TaskDisposition.Aggressive, Alert(sim.Ship).Response);
    }

    [Fact]
    public void NoTaskBefore_MeansADefensiveAlert()
    {
        var (sim, attacker) = Setup();
        using var _ = sim;
        sim.Ship.Navigator.Clear();

        sim.Ship.ApplyDamage(Shot(attacker));

        Assert.Null(Alert(sim.Ship).Previous);
        Assert.Equal(TaskDisposition.Defensive, Alert(sim.Ship).Response);
    }

    [Fact]
    public void ShotsThatDoNotAlert()
    {
        var (sim, attacker) = Setup();
        using var _ = sim;
        StartPatrol(sim);

        sim.Ship.ApplyDamage(Shot(sim.Ship));   // its own shot
        sim.Ship.ApplyDamage(Shot(null));       // no owner
        Assert.False(sim.Ship.Navigator.IsAlerting);

        sim.Ship.CurrentShieldStrength = 0; // an active shield would absorb even a huge hit
        sim.Ship.ApplyDamage(Shot(attacker, damage: 1000)); // a killing shot: nothing left to alert
        Assert.True(sim.Ship.IsDestroyed);
        Assert.False(sim.Ship.Navigator.IsAlerting);
    }

    [Fact]
    public void ThePlayerIsNotPutOnAlert()
    {
        var (sim, _) = Setup();
        using var _ = sim;
        var player = sim.Universe.Player;
        player.Navigator.Clear();

        player.ApplyDamage(Shot(sim.Ship));

        Assert.False(player.Navigator.IsAlerting);
        Assert.Empty(player.Hostiles);
    }

    [Fact]
    public void ASecondAttackerJoinsTheRunningAlert_WithoutNestingIt()
    {
        var (sim, attacker) = Setup();
        using var _ = sim;
        var patrol = StartPatrol(sim);
        var second = new Nonplayer("second");
        sim.System.AddNpc(second);

        sim.Ship.ApplyDamage(Shot(attacker));
        var alert = Alert(sim.Ship);
        sim.Ship.ApplyDamage(Shot(second));

        Assert.Same(alert, Alert(sim.Ship));
        Assert.Same(patrol, alert.Previous);
        Assert.True(sim.Ship.IsHostileTo(attacker) && sim.Ship.IsHostileTo(second));
    }

    [Fact]
    public void ATaskThatCannotBeSuspended_FinishesFirst_ThenTheAlertStarts()
    {
        var (sim, attacker) = Setup();
        using var _ = sim;
        sim.Ship.Navigator.Clear();
        // A jump that is already past its autopilot stage moves the ship by script.
        sim.Place(new Vector2(sim.System.SystemRadius * 0.5f, 0));
        sim.Ship.Rotation = 0f;
        var jump = sim.Start(new JumpTask(sim.Ship, null, null, JumpTask.Phase.Jump));
        sim.Ship.Navigator.Update(NavSim.Dt);
        Assert.False(jump.CanBeInterrupted);

        sim.Ship.ApplyDamage(Shot(attacker));
        Assert.False(sim.Ship.Navigator.IsAlerting);              // not now...
        Assert.Same(jump, sim.Ship.Navigator.ActiveTask);

        sim.Run(60f, () => jump.IsFinished);
        sim.Ship.Navigator.Update(NavSim.Dt);

        Assert.True(sim.Ship.Navigator.IsAlerting);               // ...but as soon as the jump is over
        Assert.Null(Alert(sim.Ship).Previous);
    }

    // -- ending and resuming ------------------------------------------------

    [Fact]
    public void WhenTheHostileIsGone_TheAlertEnds_AndThePreviousTaskResumesWhereItWas()
    {
        var (sim, attacker) = Setup();
        using var _ = sim;
        var patrol = StartPatrol(sim);
        sim.Run(5f);
        int waypoint = patrol.CurrentWaypointIndex;

        sim.Ship.ApplyDamage(Shot(attacker));
        attacker.CurrentHullStrength = 0; // the attacker is destroyed
        sim.Run(1f);

        Assert.False(sim.Ship.Navigator.IsAlerting);
        Assert.Same(patrol, sim.Ship.Navigator.ActiveTask);
        Assert.Equal(waypoint, patrol.CurrentWaypointIndex);
        Assert.False(sim.Ship.IsHostileTo(attacker)); // destroyed ships are forgotten
    }

    [Fact]
    public void QueuedTasksWait_UntilTheAlertAndTheInterruptedTaskAreDone()
    {
        var (sim, attacker) = Setup();
        using var _ = sim;
        var patrol = StartPatrol(sim);
        var later = sim.Start(new PatrolTask(sim.Ship, new List<Vector2> { new(0, 0) }));

        sim.Ship.ApplyDamage(Shot(attacker));
        attacker.CurrentHullStrength = 0;
        sim.Run(1f);

        Assert.Same(patrol, sim.Ship.Navigator.ActiveTask);
        Assert.Equal(1, sim.Ship.Navigator.QueuedCount);
        Assert.False(later.IsFinished);
    }

    [Fact]
    public void ClearingTheNavigator_DropsAnAlertAndTheTaskItKept()
    {
        var (sim, attacker) = Setup();
        using var _ = sim;
        StartPatrol(sim);
        sim.Ship.ApplyDamage(Shot(attacker));

        sim.Ship.Navigator.Clear();
        sim.Run(1f);

        Assert.False(sim.Ship.Navigator.HasActiveTask);
    }

    // -- aggressive response ------------------------------------------------

    [Fact]
    public void AggressiveTask_HuntsTheAttackerDownAndShootsIt()
    {
        var (sim, attacker) = Setup();
        using var _ = sim;
        attacker.Position = new Vector2(9000, 3000);
        attacker.Velocity = Vector2.Zero;
        sim.Place(new Vector2(4000, 0));
        StartPatrol(sim);
        int hullBefore = attacker.CurrentHullStrength.Value;

        sim.Ship.ApplyDamage(Shot(attacker));
        var phases = new HashSet<AlertTask.Phase>();
        sim.RunSystem(sim.TravelTime(6000f) + 30f, () => attacker.CurrentHullStrength < hullBefore, () =>
        {
            if (Alert(sim.Ship) is { } alert) phases.Add(alert.CurrentPhase);
        });

        Assert.True(attacker.CurrentHullStrength < hullBefore, "the aggressive NPC should have hit the attacker");
        Assert.Contains(AlertTask.Phase.Pursue, phases);   // it had to close the distance first
        Assert.DoesNotContain(AlertTask.Phase.Flee, phases);
    }

    [Fact]
    public void AggressiveTask_UsingAGuard_AlsoHunts()
    {
        var (sim, attacker) = Setup();
        using var _ = sim;
        var planet = sim.System.Planets[0];
        attacker.Position = planet.Position + new Vector2(5000, 0);
        attacker.Velocity = Vector2.Zero;
        sim.Place(planet.Position + new Vector2(0, 600));
        var guard = sim.Start(new GuardTask(sim.Ship, planet.Position, planet.Radius * 2f));
        sim.Ship.Navigator.Update(NavSim.Dt);
        Assert.Equal(TaskDisposition.Aggressive, guard.Disposition);
        int hullBefore = attacker.CurrentHullStrength.Value;

        sim.Ship.ApplyDamage(Shot(attacker));
        sim.RunSystem(90f, () => attacker.CurrentHullStrength < hullBefore);

        Assert.True(attacker.CurrentHullStrength < hullBefore);
    }

    [Fact]
    public void AnUnarmedShip_CannotAttack_SoItFlees_EvenWhenAggressive()
    {
        var (sim, attacker) = Setup();
        using var _ = sim;
        sim.Ship.Equipment.Clear();
        StartPatrol(sim);

        sim.Ship.ApplyDamage(Shot(attacker));
        sim.Run(0.1f);

        Assert.Equal(AlertTask.Phase.Flee, Alert(sim.Ship).CurrentPhase);
    }

    // -- defensive response -------------------------------------------------

    [Fact]
    public void DefensiveTask_WithAFarAwayHostile_LeavesTheSystem_WithoutFiring()
    {
        var (sim, attacker) = Setup();
        using var _ = sim;
        var planet = sim.System.Planets[^1];
        attacker.Position = new Vector2(0, -sim.System.SystemRadius * 0.9f); // far from the NPC
        attacker.Velocity = Vector2.Zero;
        sim.Place(planet.Position + new Vector2(2000, 0));
        var dock = sim.Start(new DockTask(sim.Ship, planet.Position, holdDuration: 600f));
        sim.Ship.Navigator.Update(NavSim.Dt);
        Assert.Equal(TaskDisposition.Defensive, dock.Disposition);
        int hullBefore = attacker.CurrentHullStrength.Value;

        sim.Ship.ApplyDamage(Shot(attacker));
        var phases = new HashSet<AlertTask.Phase>();
        sim.RunSystem(180f, () => sim.Ship.Remove, () =>
        {
            if (Alert(sim.Ship) is { } alert) phases.Add(alert.CurrentPhase);
        });

        Assert.True(sim.Ship.Remove, "the NPC should have jumped out of the system");
        Assert.Equal(new[] { AlertTask.Phase.Flee }, phases.ToArray());
        Assert.Equal(hullBefore, attacker.CurrentHullStrength); // never fired
    }

    [Fact]
    public void DefensiveTask_WithAHostileInRange_ShootsBack_InsteadOfFleeing()
    {
        var (sim, attacker) = Setup();
        using var _ = sim;
        var planet = sim.System.Planets[^1];
        sim.Place(planet.Position + new Vector2(2000, 0));
        attacker.Position = sim.Ship.Position + new Vector2(0, 700); // well inside weapon range
        attacker.Velocity = Vector2.Zero;
        sim.Start(new DockTask(sim.Ship, planet.Position, holdDuration: 600f));
        sim.Ship.Navigator.Update(NavSim.Dt);
        int hullBefore = attacker.CurrentHullStrength.Value;

        sim.Ship.ApplyDamage(Shot(attacker));
        var phases = new HashSet<AlertTask.Phase>();
        sim.RunSystem(60f, () => attacker.CurrentHullStrength < hullBefore, () =>
        {
            if (Alert(sim.Ship) is { } alert) phases.Add(alert.CurrentPhase);
        });

        Assert.True(attacker.CurrentHullStrength < hullBefore, "should have shot back");
        Assert.Contains(AlertTask.Phase.Engage, phases);
        Assert.DoesNotContain(AlertTask.Phase.Pursue, phases); // defensive ships do not chase
    }

    [Fact]
    public void ADefensiveShipAlreadyJumping_JustCarriesOnJumping()
    {
        var (sim, attacker) = Setup();
        using var _ = sim;
        attacker.Position = new Vector2(0, -sim.System.SystemRadius * 0.9f);
        sim.Place(new Vector2(1000, 0));
        var jump = sim.Start(new JumpTask(sim.Ship)); // a merchant on its way out
        sim.Ship.Navigator.Update(NavSim.Dt);
        sim.Run(1f);

        sim.Ship.ApplyDamage(Shot(attacker));
        sim.RunSystem(sim.TravelTime(sim.System.SystemRadius * 1.5f), () => sim.Ship.Remove);

        Assert.True(sim.Ship.Remove);
        Assert.True(jump.IsFinished); // it was the original jump that finished, not a second one
    }
}
