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

    // -- finding the nearest spaceport ---------------------------------------------

    private static Planet[] TwoSpaceports() =>
        Enumerable.Range(0, 1000).Select(i => new Planet($"find_Planet_{i}", 90f, 200f, 3000f, 9000f, 1 + i % 8, 8))
                  .Where(p => p.Spaceport != null).Take(2).ToArray();

    [Fact]
    public void NearestSpaceport_IsTheClosestToTheSurface_WhateverTheRange()
    {
        using var sim = new NavSim();
        var ports = TwoSpaceports();
        ports[0].Position = new Vector2(0f, 0f);
        ports[1].Position = new Vector2(1000000f, 0f);
        sim.System.Planets.Clear();
        sim.System.Planets.AddRange(new[] { ports[1], ports[0] });

        Assert.Same(ports[0], sim.System.NearestSpaceport(new Vector2(400000f, 0f)));
        Assert.Same(ports[1], sim.System.NearestSpaceport(new Vector2(600000f, 0f)));
        Assert.Same(ports[0], sim.System.NearestSpaceport(new Vector2(0f, 90000f))); // far beyond any docking range, still the nearest
    }

    [Fact]
    public void NearestSpaceport_SkipsPlanetsWithoutOne_AndIsNullWhenThereAreNone()
    {
        using var sim = new NavSim();
        var withPort = TwoSpaceports()[0];
        var without = Enumerable.Range(0, 1000).Select(i => new Planet($"find_none_{i}", 90f, 200f, 3000f, 9000f, 1 + i % 8, 8))
                                .First(p => p.Spaceport == null);
        withPort.Position = new Vector2(8000f, 0f);
        without.Position = Vector2.Zero;
        sim.System.Planets.Clear();
        sim.System.Planets.AddRange(new[] { without, withPort });

        Assert.Same(withPort, sim.System.NearestSpaceport(Vector2.Zero)); // the planet with no spaceport is closer, and ignored

        sim.System.Planets.Remove(withPort);
        Assert.Null(sim.System.NearestSpaceport(Vector2.Zero));
    }
}
