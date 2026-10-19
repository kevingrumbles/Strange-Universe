using System.Linq;
using Microsoft.Xna.Framework;
using Strange_Universe.Game.Entities;
using Strange_Universe.Game.Entities.ShipParts;
using Strange_Universe.Tests.Fakes;
using Xunit;

namespace Strange_Universe.Tests;

/// <summary>The planet decides whether a ship can dock: spaceport, range from the surface, and a flat speed limit.</summary>
public class DockingCheckTests
{
    private static Planet PlanetWith(bool spaceport) =>
        Enumerable.Range(0, 1000)
                  .Select(i => new Planet($"dock_Planet_{i}", 90f, 200f, 3000f, 9000f, 1 + i % 8, 8))
                  .First(p => (p.Spaceport != null) == spaceport);

    /// <summary>A ship <paramref name="surfaceDistance"/> from the planet's surface, moving at <paramref name="speed"/>.</summary>
    private static Nonplayer ShipAt(Planet planet, float surfaceDistance, float speed = 0f)
    {
        var ship = new Nonplayer("docker") { Position = planet.Position + new Vector2(planet.Radius + surfaceDistance, 0f) };
        ship.Velocity = new Vector2(0f, speed);
        return ship;
    }

    [Fact]
    public void ANearlyStoppedShip_InRange_CanDock()
    {
        var planet = PlanetWith(spaceport: true);

        Assert.Equal(DockingCheck.Ok, planet.CheckDocking(ShipAt(planet, surfaceDistance: 50f)));
        Assert.Equal(DockingCheck.Ok, planet.CheckDocking(ShipAt(planet, surfaceDistance: 0f)));
    }

    [Fact]
    public void RangeIsMeasuredFromTheSurface_NotTheCentre()
    {
        var planet = PlanetWith(spaceport: true);

        // The centre is further than the range, but the surface is not.
        Assert.True(planet.Radius + Planet.DockRange - 1f > Planet.DockRange);
        Assert.Equal(DockingCheck.Ok, planet.CheckDocking(ShipAt(planet, Planet.DockRange - 1f)));
        Assert.Equal(DockingCheck.Ok, planet.CheckDocking(ShipAt(planet, Planet.DockRange)));       // the edge is in range
        Assert.Equal(DockingCheck.OutOfRange, planet.CheckDocking(ShipAt(planet, Planet.DockRange + 1f)));
    }

    [Fact]
    public void TheSpeedLimitIsFlat_AndIndependentOfTheShipsTopSpeed()
    {
        var planet = PlanetWith(spaceport: true);

        Assert.Equal(DockingCheck.Ok, planet.CheckDocking(ShipAt(planet, 50f, speed: 0f)));
        Assert.Equal(DockingCheck.Ok, planet.CheckDocking(ShipAt(planet, 50f, speed: Planet.MaxDockSpeed)));      // the limit itself is allowed
        Assert.Equal(DockingCheck.TooFast, planet.CheckDocking(ShipAt(planet, 50f, speed: Planet.MaxDockSpeed + 0.5f)));

        // A fast ship type and a slow one face the same limit.
        var runner = new Nonplayer("fast", new Strange_Universe.Game.Components.ShipComponents.Runner()) { Position = planet.Position + new Vector2(planet.Radius + 50f, 0f) };
        runner.Velocity = new Vector2(0f, Planet.MaxDockSpeed + 0.5f);
        Assert.Equal(DockingCheck.TooFast, planet.CheckDocking(runner));
    }

    [Fact]
    public void ARangeFailure_IsReportedBeforeASpeedFailure()
    {
        var planet = PlanetWith(spaceport: true);

        Assert.Equal(DockingCheck.OutOfRange, planet.CheckDocking(ShipAt(planet, Planet.DockRange + 500f, speed: 400f)));
    }

    [Fact]
    public void APlanetWithoutASpaceport_CannotBeDockedAt()
    {
        var planet = PlanetWith(spaceport: false);

        Assert.Equal(DockingCheck.NoSpaceport, planet.CheckDocking(ShipAt(planet, 10f)));
    }

    // -- finding the planet to dock at ------------------------------------------

    private static (NavSim sim, Planet near, Planet far) SystemWithTwoSpaceports()
    {
        var sim = new NavSim();
        var withPort = Enumerable.Range(0, 1000).Select(i => new Planet($"find_Planet_{i}", 90f, 200f, 3000f, 9000f, 1 + i % 8, 8))
                                 .Where(p => p.Spaceport != null).Take(2).ToList();
        withPort[0].Position = new Vector2(0f, 0f);
        withPort[1].Position = new Vector2(1000f, 0f);
        sim.System.Planets.Clear();
        sim.System.Planets.AddRange(withPort);
        return (sim, withPort[0], withPort[1]);
    }

    [Fact]
    public void FindDockingPlanet_ReturnsTheNearestSpaceportInRange_WithItsCheck()
    {
        var (sim, near, far) = SystemWithTwoSpaceports();
        using var _ = sim;
        sim.Ship.Position = near.Position + new Vector2(near.Radius + 40f, 0f);
        sim.Ship.Velocity = Vector2.Zero;

        var option = sim.System.FindDockingPlanet(sim.Ship);

        Assert.Same(near, option.Planet);
        Assert.Equal(DockingCheck.Ok, option.Check);

        sim.Ship.Velocity = new Vector2(0f, Planet.MaxDockSpeed + 5f);
        Assert.Equal(DockingCheck.TooFast, sim.System.FindDockingPlanet(sim.Ship).Check); // still the same planet, now refused
        Assert.Same(near, sim.System.FindDockingPlanet(sim.Ship).Planet);
    }

    [Fact]
    public void FindDockingPlanet_WithNothingInRange_SaysSo()
    {
        var (sim, near, _) = SystemWithTwoSpaceports();
        using var _ = sim;
        sim.Ship.Position = near.Position + new Vector2(0f, 20000f);

        var option = sim.System.FindDockingPlanet(sim.Ship);

        Assert.Null(option.Planet);
        Assert.Equal(DockingCheck.OutOfRange, option.Check);
    }

    [Fact]
    public void FindDockingPlanet_InASystemWithNoSpaceports_SaysNoSpaceport()
    {
        using var sim = new NavSim();
        sim.System.Planets.Clear();
        sim.System.Planets.Add(PlanetWith(spaceport: false));

        var option = sim.System.FindDockingPlanet(sim.Ship);

        Assert.Null(option.Planet);
        Assert.Equal(DockingCheck.NoSpaceport, option.Check);
    }
}
