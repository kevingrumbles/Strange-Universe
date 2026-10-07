using System;
using Microsoft.Xna.Framework;
using Strange_Universe.Game.Entities;
using Strange_Universe.Game.NavSystem;
using Strange_Universe.Tests.Fakes;
using Xunit;

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
        task.CurrentState = TaskState.Spawning;

        task.Update(0.016f);

        Assert.Equal(new Vector2(10, 20), npc.Position);
        Assert.Equal(TaskState.Complete, task.CurrentState);
    }

    [Fact]
    public void JumpTask_Completed_InTargetSystem_UpdatesRouteAndFuel()
    {
        var npc = new Nonplayer("n2") { CurrentFuelLevel = 3 };
        var world = new FakeWorld { SystemId = "target" };
        var task = new JumpTask(npc, null, "target", TaskState.Complete) { World = world };

        task.OnCompleted();

        Assert.Equal(new[] { "target" }, world.RemovedFromRoute);
        Assert.Equal(2, npc.CurrentFuelLevel);
    }

    [Fact]
    public void JumpTask_Completed_ElsewhereDoesNothing()
    {
        var npc = new Nonplayer("n3") { CurrentFuelLevel = 3 };
        var world = new FakeWorld { SystemId = "somewhere-else" };
        var task = new JumpTask(npc, null, "target", TaskState.Complete) { World = world };

        task.OnCompleted();

        Assert.Empty(world.RemovedFromRoute);
        Assert.Equal(3, npc.CurrentFuelLevel);
    }

    [Fact]
    public void JumpTask_SystemTranslation_EntersTargetAndPlacesShipAtEdge()
    {
        var player = new Player("p", "Shuttle") { Rotation = 0f }; // outbound heading from the jump
        var world = new FakeWorld { EdgeEntry = new Vector2(5000, 0) };
        world.KnownSystems.Add("target");
        var task = new JumpTask(player, null, "target") { World = world }; // constructor validates against the owner's own system, so set the state afterwards
        task.CurrentState = TaskState.SystemTranslation;

        task.Update(0.016f);

        Assert.Equal(new[] { "target" }, world.Entered);
        Assert.Equal(new Vector2(5000, 0), player.Position);
        Assert.True(player.Velocity.X < 0); // heading inward
        Assert.Equal(MathF.PI, MathF.Abs(player.Rotation), 3); // and facing inward (toward -X) on arrival
        Assert.Equal(TaskState.ArriveInSystem, task.CurrentState);
    }

    private static (JumpTask task, Player player, FakeWorld world) Arriving(Vector2 position)
    {
        var player = new Player("p", "Shuttle") { Position = position };
        var world = new FakeWorld { SystemRadius = 10000f };
        var task = new JumpTask(player, null, "target") { World = world };
        task.CurrentState = TaskState.ArriveInSystem;
        return (task, player, world);
    }

    [Fact]
    public void Arrival_StartsMuchFasterThanNormal_AtTheEdge()
    {
        var (task, player, _) = Arriving(new Vector2(10000, 0));
        task.Update(0.016f);

        Assert.Equal(player.ShipType.MaxSpeed * JumpTask.ArrivalSpeedMultiplier, player.Speed, 1);
        Assert.True(player.Velocity.X < 0);
        Assert.Equal(TaskState.ArriveInSystem, task.CurrentState);
    }

    [Fact]
    public void Arrival_Tapers_ThenCompletesAtNormalSpeedHalfwayToCenter()
    {
        float max = new Player("p", "Shuttle").ShipType.MaxSpeed;
        float last = float.MaxValue;
        foreach (float x in new[] { 10000f, 9000f, 8000f, 7000f, 6000f, 5100f })
        {
            var (task, player, _) = Arriving(new Vector2(x, 0));
            task.Update(0.016f);
            Assert.True(player.Speed < last, $"speed should fall as the ship moves in (x={x})");
            Assert.True(player.Speed > max, $"still above normal before the halfway point (x={x})");
            Assert.Equal(TaskState.ArriveInSystem, task.CurrentState);
            last = player.Speed;
        }

        var (done, ship, _) = Arriving(new Vector2(5000, 0)); // exactly halfway
        done.Update(0.016f);
        Assert.Equal(max, ship.Speed, 1);
        Assert.Equal(TaskState.Complete, done.CurrentState);
    }
}
