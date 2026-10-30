using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Strange_Universe.Game.Components;
using Strange_Universe.Game.Entities;
using Strange_Universe.Game.Entities.ShipParts;
using Strange_Universe.Tests.Fakes;
using Xunit;
using static Strange_Universe.Tests.Fakes.DockedWorld;

namespace Strange_Universe.Tests;

/// <summary>Docking at a spaceport: the L key, the docked state, undocking, and what is saved.</summary>
public class DockingStateTests
{
    private const float Dt = 1f / 60f;

    private static string FirstMessage(Universe u, Action act)
    {
        var messages = new List<string>();
        using var _ = u.Events.Subscribe<MessageRequested>(m => messages.Add(m.Message));
        act();
        return messages.FirstOrDefault();
    }

    // -- the L key: the first press targets, the next one docks -----------------------

    [Fact]
    public void PressingDock_WithNothingTargeted_TargetsTheSpaceport_AndDoesNotDock()
    {
        var planet = PlanetWith(spaceport: true);
        var u = Started(planet);

        string message = FirstMessage(u, () => u.Update(Dt, PressDock));

        Assert.False(u.IsDocked);
        Assert.Same(planet, u.Player.DockTarget);
        Assert.Equal($"Targeted {planet.Name}", message);
        u.Dispose();
    }

    [Fact]
    public void PressingDockAgain_NearlyStopped_InRange_Docks_AndClearsTheTarget()
    {
        var planet = PlanetWith(spaceport: true);
        var u = Started(planet);
        var docked = new List<PlayerDocked>();
        using var _ = u.Events.Subscribe<PlayerDocked>(docked.Add);

        u.Update(Dt, PressDock); // target
        u.Update(Dt, PressDock); // dock

        Assert.True(u.IsDocked);
        Assert.Same(planet, u.DockedAt);
        Assert.Single(docked);
        Assert.Same(planet, docked[0].Planet);
        Assert.Null(u.Player.DockTarget);
        u.Dispose();
    }

    [Fact]
    public void PressingDock_TargetsTheNearestSpaceport_WhereverItIs()
    {
        var near = PlanetWith(spaceport: true);
        var far = Enumerable.Range(0, 1000).Select(i => new Planet($"other_Planet_{i}", 90f, 200f, 3000f, 9000f, 1 + i % 8, 8))
                            .First(p => p.Spaceport != null);
        var u = Started(near, surfaceDistance: 4000f); // well out of docking range of both
        far.Position = new Vector2(-9000f, 6000f);
        u.ActiveStarSystem.Planets.Add(far);
        var noPort = PlanetWith(spaceport: false);
        noPort.Position = u.Player.Position + new Vector2(5f, 0f); // right next to the player, but no spaceport
        u.ActiveStarSystem.Planets.Add(noPort);

        u.Update(Dt, PressDock);

        Assert.Same(near, u.Player.DockTarget);
        u.Dispose();
    }

    [Fact]
    public void PressingDock_InASystemWithNoSpaceport_SaysSo_AndTargetsNothing()
    {
        var noPort = Started(PlanetWith(spaceport: false));

        string message = FirstMessage(noPort, () => noPort.Update(Dt, PressDock));

        Assert.Equal("No spaceport in this system", message);
        Assert.Null(noPort.Player.DockTarget);
        Assert.False(noPort.IsDocked);
        noPort.Dispose();
    }

    [Fact]
    public void PressingDockAgain_TooFast_RefusesWithAMessage_AndKeepsTheTarget()
    {
        var planet = PlanetWith(spaceport: true);
        var u = Started(planet, speed: Planet.MaxDockSpeed + 50f);
        u.Update(Dt, PressDock);

        string message = FirstMessage(u, () => u.Update(Dt, PressDock));

        Assert.False(u.IsDocked);
        Assert.Equal("Too fast to dock", message);
        Assert.Same(planet, u.Player.DockTarget);
        u.Dispose();
    }

    [Fact]
    public void PressingDockAgain_OutOfRange_RefusesWithAMessage_AndKeepsTheTarget()
    {
        var planet = PlanetWith(spaceport: true);
        var u = Started(planet, surfaceDistance: Planet.DockRange + 2000f);
        u.Update(Dt, PressDock);

        string message = FirstMessage(u, () => u.Update(Dt, PressDock));

        Assert.False(u.IsDocked);
        Assert.Equal($"{planet.Name} is out of docking range", message);
        Assert.Same(planet, u.Player.DockTarget);
        u.Dispose();
    }

    [Fact]
    public void WithoutPressingDock_NothingHappens()
    {
        var u = Started(PlanetWith(spaceport: true));

        u.Update(Dt, new InputState());

        Assert.False(u.IsDocked);
        Assert.Null(u.Player.DockTarget);
        u.Dispose();
    }

    [Fact]
    public void TargetingAShip_KeepsTheSpaceportTargetedForLanding()
    {
        foreach (var key in new[] { new InputState { CycleTarget = true }, new InputState { TargetNearest = true } })
        {
            var planet = PlanetWith(spaceport: true);
            var u = Started(planet);
            u.ActiveStarSystem.AddNpc(new Nonplayer("other-ship") { Position = u.Player.Position + new Vector2(0f, 400f) });
            u.Update(Dt, PressDock);
            Assert.Same(planet, u.Player.DockTarget);

            u.Update(Dt, key);

            Assert.NotNull(u.Player.Target);
            Contains(u);
            Assert.Same(planet, u.Player.DockTarget); // the two targets are separate: the ship area only ever holds ships
            u.Dispose();
        }

        static void Contains(Universe u) =>
            Assert.Contains(u.ActiveStarSystem.Npcs, n => ReferenceEquals(n, u.Player.Target)); // the ship target is a ship
    }

    [Fact]
    public void TargetingAPlanet_DoesNotChangeTheTargetedShip()
    {
        var u = Started(PlanetWith(spaceport: true));
        var other = new Nonplayer("other-ship") { Position = u.Player.Position + new Vector2(0f, 400f) };
        u.ActiveStarSystem.AddNpc(other);
        u.Player.SetTarget(other);

        u.Update(Dt, PressDock); // targets the spaceport

        Assert.NotNull(u.Player.DockTarget);
        Assert.Same(other, u.Player.Target);
        u.Dispose();
    }

    [Fact]
    public void TheTarget_IsForgottenWhenThePlayerIsInAnotherSystem()
    {
        var u = Started(PlanetWith(spaceport: true));
        u.Update(Dt, PressDock);
        Assert.NotNull(u.Player.DockTarget);

        u.EnterSystem(u.StarSystemNodes[0]); // the system is built again, so the old planet is not in it

        Assert.Null(u.Player.DockTarget);
        u.Dispose();
    }

    // -- docked state ------------------------------------------------------------------

    [Fact]
    public void Docking_ClearsTheActiveSystem_StopsThePlayer_AndKeepsWhereTheyWere()
    {
        var planet = PlanetWith(spaceport: true);
        var u = Started(planet, speed: Planet.MaxDockSpeed * 0.5f);
        string systemId = u.ActiveStarSystem.SystemId;
        Vector2 where = u.Player.Position;
        u.Player.EnqueueNavTask(new Strange_Universe.Game.NavSystem.SpawnTask(u.Player));

        u.Dock(planet);

        Assert.Throws<InvalidOperationException>(() => u.ActiveStarSystem);
        Assert.Null(u.Player.StarSystem);
        Assert.Equal(Vector2.Zero, u.Player.Velocity);
        Assert.False(u.Player.HasActiveNavTask);
        Assert.Equal(where, u.Player.Position);
        Assert.Equal(where, u.Player.Location);            // the saved position in the system
        Assert.Equal(systemId, u.Player.CurrentStarSystemID);
        u.Dispose();
    }

    [Fact]
    public void WhileDocked_UpdatingTheUniverseIsSafe_AndDoesNothing()
    {
        var planet = PlanetWith(spaceport: true);
        var u = Started(planet);
        u.Dock(planet);
        Vector2 where = u.Player.Position;

        u.Update(Dt, new InputState { Thrust = true, Fire = true });

        Assert.Equal(where, u.Player.Position);
        Assert.True(u.IsDocked);
        u.Dispose();
    }

    [Fact]
    public void Docking_NeedsASpaceport_AndDockingTwiceIsHarmless()
    {
        var planet = PlanetWith(spaceport: true);
        var u = Started(planet);
        Assert.Throws<ArgumentException>(() => u.Dock(PlanetWith(spaceport: false)));
        Assert.Throws<ArgumentNullException>(() => u.Dock(null));

        var docked = new List<PlayerDocked>();
        using var _ = u.Events.Subscribe<PlayerDocked>(docked.Add);
        u.Dock(planet);
        u.Dock(planet);

        Assert.Single(docked);
        u.Dispose();
    }

    // -- undocking ----------------------------------------------------------------------

    [Fact]
    public void Undocking_BringsBackTheSystem_AndPutsThePlayerWhereTheyDockedAtRest_WithFullShields()
    {
        var planet = PlanetWith(spaceport: true);
        var u = Started(planet);
        TestGear.Arm(u.Player, TestGear.Shield(30f), TestGear.Plating(10f));
        string systemId = u.ActiveStarSystem.SystemId;
        Vector2 where = u.Player.Position;
        u.Dock(planet);
        u.Player.CurrentShieldStrength = 0; // however it got that way, it comes back on entering the system
        var undocked = new List<PlayerUndocked>();
        using var _ = u.Events.Subscribe<PlayerUndocked>(undocked.Add);

        u.Undock();

        Assert.False(u.IsDocked);
        Assert.Equal(systemId, u.ActiveStarSystem.SystemId);
        Assert.Same(u.ActiveStarSystem, u.Player.StarSystem);
        Assert.Equal(where, u.Player.Position);
        Assert.Equal(Vector2.Zero, u.Player.Velocity);
        Assert.Equal(u.Player.MaxShieldStrength, u.Player.CurrentShieldStrength);
        Assert.Single(undocked);
        u.Dispose();
    }

    [Fact]
    public void UndockingWhenNotDocked_DoesNothing()
    {
        var u = Started(PlanetWith(spaceport: true));
        var system = u.ActiveStarSystem;
        var undocked = new List<PlayerUndocked>();
        using var _ = u.Events.Subscribe<PlayerUndocked>(undocked.Add);

        u.Undock();

        Assert.Same(system, u.ActiveStarSystem);
        Assert.Empty(undocked);
        u.Dispose();
    }

    [Fact]
    public void AfterUndocking_ThePlayerCanDockAgain()
    {
        var planet = PlanetWith(spaceport: true);
        var u = Started(planet);
        u.Dock(planet);
        u.Undock();
        // the system was rebuilt, so the planet is a new object there
        var rebuiltPlanet = u.ActiveStarSystem.Planets.First(p => p.Spaceport != null);
        u.Player.Position = rebuiltPlanet.Position + new Vector2(rebuiltPlanet.Radius + 40f, 0f);

        u.Update(Dt, PressDock); // target
        u.Update(Dt, PressDock); // dock

        Assert.True(u.IsDocked);
        u.Dispose();
    }

    // -- saving ---------------------------------------------------------------------------

    private static string TempPath() => Path.Combine(Path.GetTempPath(), $"su-dock-{Guid.NewGuid():N}.json");

    [Fact]
    public void ADockedGame_IsSavedAtTheDockingPosition_AndLoadsFlyingNotDocked()
    {
        string path = TempPath();
        try
        {
            var planet = PlanetWith(spaceport: true);
            var u = Started(planet);
            string systemId = u.ActiveStarSystem.SystemId;
            Vector2 where = u.Player.Position;
            u.Dock(planet);
            new FileSaveService(path).Save(u);
            u.Dispose();

            var loaded = new FileSaveService(path).LoadAll().Single();
            loaded.Generate(null);

            Assert.False(loaded.IsDocked);                              // the player starts in the ship
            Assert.Equal(systemId, loaded.ActiveStarSystem.SystemId);   // in the system they docked in
            Assert.Equal(where.X, loaded.Player.Position.X, 2);         // at the place they docked
            Assert.Equal(where.Y, loaded.Player.Position.Y, 2);
            Assert.False(loaded.Player.HasActiveNavTask);               // placed already, so no spawn task moves them
            loaded.Dispose();
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    // -- shields ---------------------------------------------------------------------------

    [Fact]
    public void EnteringASystem_RechargesTheShieldsToFull()
    {
        var u = new Universe("Shields", "shield-seed");
        TestGear.Arm(u.Player, TestGear.Shield(30f), TestGear.Shield(20f));
        u.Player.CurrentShieldStrength = 3;

        u.Generate(null); // loading into the starting system

        Assert.Equal(50, u.Player.MaxShieldStrength);
        Assert.Equal(50, u.Player.CurrentShieldStrength);

        u.Player.CurrentShieldStrength = 0;
        u.EnterSystem(u.StarSystemNodes[0]); // and again on any other entry
        Assert.Equal(50, u.Player.CurrentShieldStrength);
        u.Dispose();
    }

    // -- the HUD hint ------------------------------------------------------------------------

    [Fact]
    public void TheDockingHint_FollowsTheTarget_NamesThePlanet_AndAsksForLessSpeedWhenTooFast()
    {
        var planet = PlanetWith(spaceport: true);
        var u = Started(planet);
        Assert.Null(u.DockingHint()); // nothing targeted yet

        u.Update(Dt, PressDock);      // target it
        Assert.Equal($"L: dock at {planet.Name}", u.DockingHint());

        u.Player.Velocity = new Vector2(0f, Planet.MaxDockSpeed + 20f);
        Assert.Equal($"Slow down to dock at {planet.Name}", u.DockingHint());

        u.Player.Position = planet.Position + new Vector2(planet.Radius + Planet.DockRange + 1000f, 0f);
        Assert.Null(u.DockingHint()); // out of range: no hint

        u.Player.Velocity = Vector2.Zero;
        u.Player.Position = planet.Position + new Vector2(planet.Radius + 10f, 0f);
        u.Dock(planet);
        Assert.Null(u.DockingHint()); // nothing to hint at while docked
        u.Dispose();
    }
}
