using Strange_Universe.Game.Components;
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Strange_Universe.Game.Entities;
using Strange_Universe.Game.NavSystem;
using Strange_Universe.Tests.Fakes;
using Xunit;

namespace Strange_Universe.Tests;

/// <summary>The three stages of a jump, each run on its own, and how <see cref="JumpTask"/> hands over between them.</summary>
public class JumpStageTests
{
    // -- PrepareJumpTask (flown) --------------------------------------------

    [Fact]
    public void Prepare_GetsTheShipReadyToJump_ThenCompletes()
    {
        using var sim = new NavSim();
        sim.Place(new Vector2(1000, 0));
        var prepare = sim.Start(new PrepareJumpTask(sim.Ship));

        var phases = new List<PrepareJumpTask.Phase>();
        sim.Run(sim.TravelTime(sim.System.SystemRadius), () => prepare.Status == NavTaskStatus.Complete, () =>
        {
            if (phases.Count == 0 || phases[^1] != prepare.CurrentPhase) phases.Add(prepare.CurrentPhase);
        });

        Assert.Equal(NavTaskStatus.Complete, prepare.Status);
        Assert.Equal(new[] { PrepareJumpTask.Phase.MoveToMandeville, PrepareJumpTask.Phase.Decelerate, PrepareJumpTask.Phase.AlignForJump }, phases);
        Assert.True(sim.Ship.Position.Length() > sim.System.MandevilleRadius, "must be beyond the Mandeville radius");
        Assert.True(sim.Ship.Speed <= PrepareJumpTask.SafeJumpSpeed + 1f, $"speed {sim.Ship.Speed:F0}");
        var outward = Vector2.Normalize(sim.Ship.Position);
        Assert.True(Vector2.Dot(outward, sim.Ship.Forward) > 0.99f, "must face away from the center");
    }

    [Theory]
    [InlineData(PrepareJumpTask.Phase.MoveToMandeville)]
    [InlineData(PrepareJumpTask.Phase.Decelerate)]
    [InlineData(PrepareJumpTask.Phase.AlignForJump)]
    public void Prepare_CanStartInAnyPhase(PrepareJumpTask.Phase phase)
    {
        var task = new PrepareJumpTask(new Nonplayer("n"), phase);
        Assert.Equal(phase, task.CurrentPhase);
        Assert.Equal(NavTaskStatus.Running, task.Status);
    }

    // -- JumpDriveTask (scripted) -------------------------------------------

    [Fact]
    public void Drive_AcceleratesAlongTheFacing_UntilBeyondTheSystemEdge()
    {
        using var sim = new NavSim();
        float radius = sim.System.SystemRadius;
        sim.Place(new Vector2(sim.System.MandevilleRadius + 500f, 0));
        sim.Ship.Rotation = 0f; // facing outward (+X)
        var drive = sim.Start(new JumpDriveTask(sim.Ship));

        sim.Run(60f, () => drive.Status == NavTaskStatus.Complete);

        Assert.Equal(NavTaskStatus.Complete, drive.Status);
        Assert.True(sim.Ship.Position.Length() > radius);
        Assert.True(sim.Ship.Speed > sim.Ship.ShipType.MaxSpeed * 2f, "hyperspace speed is well above normal");
        Assert.False(sim.Ship.Remove, "removing an NPC is the whole jump's decision, not the drive's");
        Assert.Equal(0f, sim.Ship.Velocity.Y, 3); // straight along the facing
    }

    // -- ArrivalTask (scripted) ---------------------------------------------

    [Fact]
    public void Arrival_EntersTheTargetAndPlacesTheShipAtTheEdge_ThenTapers()
    {
        var player = new Player("p", ShipStats.Shuttle);
        var world = new FakeWorld { SystemRadius = 10000f, EdgeEntry = new Vector2(10000, 0) };
        world.KnownSystems.Add("target");
        var arrival = new ArrivalTask(player, null, "target") { World = world };

        arrival.Update(0.016f);

        Assert.Equal(new[] { "target" }, world.Entered);
        Assert.Equal(ArrivalTask.Phase.Taper, arrival.CurrentPhase);
        Assert.Equal(new Vector2(10000, 0), player.Position);
        Assert.Equal(player.ShipType.MaxSpeed * ArrivalTask.SpeedMultiplier, player.Speed, 1);
        Assert.Equal(NavTaskStatus.Running, arrival.Status);

        player.Position = new Vector2(ArrivalTask.TaperEndFraction * 10000f, 0);
        arrival.Update(0.016f);

        Assert.Equal(NavTaskStatus.Complete, arrival.Status);
        Assert.Equal(player.ShipType.MaxSpeed, player.Speed, 1);
    }

    [Fact]
    public void Arrival_ForAnNpc_DoesNotSwitchSystems()
    {
        var npc = new Nonplayer("n");
        var world = new FakeWorld { EdgeEntry = new Vector2(9000, 0) };
        var arrival = new ArrivalTask(npc, null, "target") { World = world };

        arrival.Update(0.016f);

        Assert.Empty(world.Entered);
        Assert.Equal(new Vector2(9000, 0), npc.Position);
    }

    // -- JumpTask hand-over -------------------------------------------------

    [Theory]
    [InlineData(JumpTask.Phase.MoveToMandeville)]
    [InlineData(JumpTask.Phase.Decelerate)]
    [InlineData(JumpTask.Phase.AlignForJump)]
    [InlineData(JumpTask.Phase.Jump)]
    [InlineData(JumpTask.Phase.SystemTranslation)]
    [InlineData(JumpTask.Phase.ArriveInSystem)]
    public void JumpTask_StartsInTheRequestedPhase(JumpTask.Phase phase)
    {
        var task = new JumpTask(new Nonplayer("n"), null, null, phase);
        Assert.Equal(phase, task.CurrentPhase);
    }

    [Fact]
    public void JumpTask_Npc_IsRemovedOnlyAfterTheDriveStage_AndNeverArrives()
    {
        using var sim = new NavSim();
        sim.Place(new Vector2(sim.System.MandevilleRadius + 1000f, 0));
        var jump = sim.Start(new JumpTask(sim.Ship));

        bool removedBeforeJumpPhase = false;
        sim.Run(60f, () => jump.Status == NavTaskStatus.Complete, () =>
        {
            if (sim.Ship.Remove && jump.CurrentPhase != JumpTask.Phase.Jump) removedBeforeJumpPhase = true;
        });

        Assert.False(removedBeforeJumpPhase);
        Assert.True(sim.Ship.Remove);
        Assert.Equal(JumpTask.Phase.Jump, jump.CurrentPhase); // it never reached arrival
    }

    [Fact]
    public void JumpTask_Player_GoesOnToArrive_AndOnlyThenCompletes()
    {
        var player = new Player("p", ShipStats.Shuttle);
        var world = new FakeWorld { SystemRadius = 10000f, MandevilleRadius = 7500f, EdgeEntry = new Vector2(10000, 0), SystemId = "from" };
        world.KnownSystems.Add("target");
        // Beyond the edge, fast, facing outward: the drive stage is already done on its first update.
        player.Position = new Vector2(10500, 0);
        player.Rotation = 0f;
        player.Velocity = new Vector2(player.ShipType.MaxSpeed * 3f, 0);
        var jump = new JumpTask(player, null, "target", JumpTask.Phase.Jump) { World = world };

        jump.Update(0.016f); // drive finishes
        Assert.Equal(NavTaskStatus.Running, jump.Status);
        Assert.Equal(JumpTask.Phase.SystemTranslation, jump.CurrentPhase);
        Assert.Empty(world.Entered);

        jump.Update(0.016f); // translation
        Assert.Equal(new[] { "target" }, world.Entered);
        Assert.Equal(JumpTask.Phase.ArriveInSystem, jump.CurrentPhase);
        Assert.Equal(NavTaskStatus.Running, jump.Status);

        player.Position = new Vector2(4000, 0); // past the taper point
        jump.Update(0.016f);
        Assert.Equal(NavTaskStatus.Complete, jump.Status);
    }

    [Fact]
    public void JumpTask_GivesItsStagesTheWorldItWasAssigned()
    {
        var player = new Player("p", ShipStats.Shuttle);
        var world = new FakeWorld { EdgeEntry = new Vector2(7777, 0) };
        world.KnownSystems.Add("target");
        var jump = new JumpTask(player, null, "target", JumpTask.Phase.SystemTranslation) { World = world };

        jump.Update(0.016f);

        Assert.Equal(new Vector2(7777, 0), player.Position); // the arrival stage used the fake world's edge position
    }

    [Fact]
    public void JumpTask_StillFailsForAnUnknownPlayerTarget()
    {
        var jump = new JumpTask(new Player("p", ShipStats.Shuttle), null, "nowhere") { World = new FakeWorld() };
        jump.Update(0.016f);
        Assert.Equal(NavTaskStatus.Invalid, jump.Status);
    }
}
