using System.Linq;
using Strange_Universe.Game.Entities;
using Strange_Universe.Game.Entities.ShipParts;
using Strange_Universe.Game.NavSystem;
using Strange_Universe.Tests.Fakes;
using Xunit;

namespace Strange_Universe.Tests;

/// <summary>Round 2, Phase 3: completion side effects belong to the task, not the navigator.</summary>
public class NavTaskCompletionTests
{
    private static (Universe universe, StarSystemNode target) Setup()
    {
        var universe = new Universe("Nav", "nav-seed") { NebulaFactory = Nebula.CreateWithoutPixels };
        universe.Generate(new RecordingAssetRequests());
        var target = universe.Galaxy.FindById(universe.ActiveStarSystem.SystemConnectionIds.First());
        universe.Player.CurrentFuelLevel = 5;
        universe.JumpRoute.Add(target.SystemId);
        return (universe, target);
    }

    /// <summary>Runs a fresh navigator (the player's own queue starts with a spawn task) until the task is cleared.</summary>
    private static ShipNavigator Run(Player player, NavTask task)
    {
        var nav = new ShipNavigator(player);
        nav.Enqueue(task);
        nav.Update(0.016f); // dequeues the task
        nav.Update(0.016f); // sees Complete / Invalid
        return nav;
    }

    [Fact]
    public void CompletedJump_InTargetSystem_RemovesRouteEntryAndUsesFuel()
    {
        var (universe, target) = Setup();
        var player = universe.Player;
        var task = new JumpTask(player, null, target.SystemId, TaskState.Complete);
        universe.EnterSystem(target);

        var nav = Run(player, task);

        Assert.DoesNotContain(target.SystemId, universe.JumpRoute);
        Assert.Equal(4, player.CurrentFuelLevel);
        Assert.False(nav.HasActiveTask);
        universe.Dispose();
    }

    [Fact]
    public void CompletedJump_NotInTargetSystem_ChangesNothing()
    {
        var (universe, target) = Setup();
        var player = universe.Player;

        var nav = Run(player, new JumpTask(player, null, target.SystemId, TaskState.Complete));

        Assert.Contains(target.SystemId, universe.JumpRoute);
        Assert.Equal(5, player.CurrentFuelLevel);
        Assert.False(nav.HasActiveTask);
        universe.Dispose();
    }

    [Fact]
    public void InvalidJump_ChangesNothing()
    {
        var (universe, target) = Setup();
        var player = universe.Player;
        var task = new JumpTask(player, null, "no-such-system");
        Assert.Equal(TaskState.Invalid, task.CurrentState);
        universe.EnterSystem(target);

        var nav = Run(player, task);

        Assert.Contains(target.SystemId, universe.JumpRoute);
        Assert.Equal(5, player.CurrentFuelLevel);
        Assert.False(nav.HasActiveTask);
        universe.Dispose();
    }

    [Fact]
    public void JumpTask_ExposesTargetSystemId()
    {
        var (universe, target) = Setup();
        Assert.Equal(target.SystemId, new JumpTask(universe.Player, null, target.SystemId).TargetSystemId);
        universe.Dispose();
    }
}
