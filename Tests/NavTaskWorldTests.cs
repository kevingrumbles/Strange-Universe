using Strange_Universe.Game.Components;
using System;
using Microsoft.Xna.Framework;
using Strange_Universe.Game.Entities;
using Strange_Universe.Game.NavSystem;
using Strange_Universe.Tests.Fakes;
using Xunit;
using Strange_Universe.Game.Components.ShipComponents;

namespace Strange_Universe.Tests;

/// <summary>Round 2 follow-up: nav tasks run against <see cref="Strange_Universe.Game.Components.IWorldContext"/>.</summary>
public class NavTaskWorldTests
{
    [Fact]
    public void SpawnTask_PlacesShipAtWorldsSafeEntry()
    {
        var npc = new Nonplayer("n1");
        var world = new FakeWorld { SafeEntry = new Vector2(10, 20) };
        var task = new SpawnTask(npc) { World = world };

        task.Update(0.016f);

        Assert.Equal(new Vector2(10, 20), npc.Position);
        Assert.Equal(NavTaskStatus.Complete, task.Status);
    }

    [Fact]
    public void JumpTask_Completed_InTargetSystem_UpdatesRouteAndFuel()
    {
        var npc = new Nonplayer("n2") { CurrentFuelLevel = 3 };
        var world = new FakeWorld { SystemId = "target" };
        var task = new JumpTask(npc, null, "target") { World = world };

        task.OnCompleted();

        Assert.Equal(new[] { "target" }, world.RemovedFromRoute);
        Assert.Equal(2, npc.CurrentFuelLevel);
    }

    [Fact]
    public void JumpTask_Completed_ElsewhereDoesNothing()
    {
        var npc = new Nonplayer("n3") { CurrentFuelLevel = 3 };
        var world = new FakeWorld { SystemId = "somewhere-else" };
        var task = new JumpTask(npc, null, "target") { World = world };

        task.OnCompleted();

        Assert.Empty(world.RemovedFromRoute);
        Assert.Equal(3, npc.CurrentFuelLevel);
    }

    [Fact]
    public void JumpTask_SystemTranslation_EntersTargetAndPlacesShipAtEdge()
    {
        var player = new Player("p", new Shuttle()) { Rotation = 0f }; // outbound heading from the jump
        var world = new FakeWorld { EdgeEntry = new Vector2(5000, 0) };
        world.KnownSystems.Add("target");
        var task = new JumpTask(player, null, "target", JumpTask.Phase.SystemTranslation) { World = world };

        task.Update(0.016f);

        Assert.Equal(new[] { "target" }, world.Entered);
        Assert.Equal(new Vector2(5000, 0), player.Position);
        Assert.True(player.Velocity.X < 0); // heading inward
        Assert.Equal(MathF.PI, MathF.Abs(player.Rotation), 3); // and facing inward (toward -X) on arrival
        Assert.Equal(JumpTask.Phase.ArriveInSystem, task.CurrentPhase);
    }

    private static (JumpTask task, Player player, FakeWorld world) Arriving(Vector2 position)
    {
        var player = new Player("p", new Shuttle()) { Position = position };
        var world = new FakeWorld { SystemRadius = 10000f };
        world.KnownSystems.Add("target");
        var task = new JumpTask(player, null, "target", JumpTask.Phase.ArriveInSystem) { World = world };
        return (task, player, world);
    }

    [Fact]
    public void Arrival_StartsMuchFasterThanNormal_AtTheEdge()
    {
        var (task, player, _) = Arriving(new Vector2(10000, 0));
        task.Update(0.016f);

        Assert.Equal(player.ShipType.Flight.MaxSpeed * JumpTask.ArrivalSpeedMultiplier, player.Speed, 1);
        Assert.True(player.Velocity.X < 0);
        Assert.Equal(JumpTask.Phase.ArriveInSystem, task.CurrentPhase);
    }

    [Fact]
    public void Arrival_Tapers_ThenCompletesAtNormalSpeedHalfwayToCenter()
    {
        float max = new Player("p", new Shuttle()).ShipType.Flight.MaxSpeed;
        float last = float.MaxValue;
        foreach (float x in new[] { 10000f, 9000f, 8000f, 7000f, 6000f, 5100f })
        {
            var (task, player, _) = Arriving(new Vector2(x, 0));
            task.Update(0.016f);
            Assert.True(player.Speed < last, $"speed should fall as the ship moves in (x={x})");
            Assert.True(player.Speed > max, $"still above normal before the halfway point (x={x})");
            Assert.Equal(JumpTask.Phase.ArriveInSystem, task.CurrentPhase);
            last = player.Speed;
        }

        var (done, ship, _) = Arriving(new Vector2(5000, 0)); // exactly halfway
        done.Update(0.016f);
        Assert.Equal(max, ship.Speed, 1);
        Assert.Equal(NavTaskStatus.Complete, done.Status);
    }
}
