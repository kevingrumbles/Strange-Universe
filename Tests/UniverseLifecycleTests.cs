using Strange_Universe.Game.Components;
using Strange_Universe.Game.Components.ShipComponents;
using Strange_Universe.Game.Entities;
using Strange_Universe.Game.Entities.ShipParts;
using Strange_Universe.Tests.Fakes;
using System;
using System.Linq;
using Xunit;

namespace Strange_Universe.Tests;

/// <summary>Round 2, Phase 2: explicit system entry, nebula pool lifecycle, detached ships.</summary>
public class UniverseLifecycleTests
{
    private static Universe NewUniverse(string seed = "life-seed") =>
        new("Life", seed);

    [Fact]
    public void ActiveStarSystem_BeforeGenerate_Throws()
    {
        var universe = NewUniverse();
        Assert.Throws<InvalidOperationException>(() => universe.ActiveStarSystem);
    }

    [Fact]
    public void ActiveStarSystem_HasNoSideEffects()
    {
        var universe = NewUniverse();
        Assert.ThrowsAny<InvalidOperationException>(() => universe.ActiveStarSystem);

        Assert.Empty(universe.StarSystemNodes);
        Assert.Null(universe.Player.StarSystem);
        Assert.Null(universe.Player.CurrentStarSystemID);
    }

    [Fact]
    public void Generate_EntersStartingSystem()
    {
        var universe = NewUniverse();
        universe.Generate(null);

        var system = universe.ActiveStarSystem;
        Assert.Equal("Sol", system.Name);
        Assert.Same(system, universe.Player.StarSystem);
        Assert.Equal(system.SystemId, universe.Player.CurrentStarSystemID);
        universe.Dispose();
    }

    [Fact]
    public void Generate_UnknownSavedSystem_FallsBackToSol()
    {
        var universe = NewUniverse();
        universe.Generate(null);
        universe.Dispose();

        universe.Player.CurrentStarSystemID = "does-not-exist";
        universe.Generate(null);

        Assert.Equal("Sol", universe.ActiveStarSystem.Name);
        Assert.Equal(universe.ActiveStarSystem.SystemId, universe.Player.CurrentStarSystemID);
        universe.Dispose();
    }

    [Fact]
    public void EnterSystem_SwitchesActiveSystemAndPlayer()
    {
        var universe = NewUniverse();
        universe.Generate(null);
        var sol = universe.ActiveStarSystem;
        var target = universe.Galaxy.FindById(sol.SystemConnectionIds.First());

        var entered = universe.EnterSystem(target);

        Assert.Same(entered, universe.ActiveStarSystem);
        Assert.NotSame(sol, entered);
        Assert.Same(entered, universe.Player.StarSystem);
        Assert.Equal(target.SystemId, universe.Player.CurrentStarSystemID);
        universe.Dispose();
    }

    [Fact]
    public void DetachedShip_Update_DoesNotThrow()
    {
        var ship = new Nonplayer("detached");
        Assert.Null(ship.StarSystem);

        var ex = Record.Exception(() => ship.Update(1f / 60f));
        Assert.Null(ex);
    }

    [Fact]
    public void DetachedShip_WithQueuedTask_DoesNotThrow()
    {
        var ship = new Nonplayer("detached");
        ship.EnqueueNavTask(new Strange_Universe.Game.NavSystem.SpawnTask(ship));

        var ex = Record.Exception(() => { for (int i = 0; i < 3; i++) ship.Update(1f / 60f); });
        Assert.Null(ex);
    }

    [Fact]
    public void DetachedPlayer_Update_DoesNotThrow()
    {
        var player = new Player("p", new Shuttle());
        var ex = Record.Exception(() => player.Update(1f / 60f, new InputState { Jump = true, Fire = true }));
        Assert.Null(ex);
    }
}
