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
        var player = new Player("p", "Shuttle");
        var world = new FakeWorld { EdgeEntry = new Vector2(5000, 0) };
        world.KnownSystems.Add("target");
        var task = new JumpTask(player, null, "target") { World = world }; // constructor validates against the owner's own system, so set the state afterwards
        task.CurrentState = TaskState.SystemTranslation;

        task.Update(0.016f);

        Assert.Equal(new[] { "target" }, world.Entered);
        Assert.Equal(new Vector2(5000, 0), player.Position);
        Assert.True(player.Velocity.X < 0); // heading inward
        Assert.Equal(TaskState.ArriveInSystem, task.CurrentState);
    }
}
