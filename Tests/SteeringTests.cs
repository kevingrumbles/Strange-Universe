using Microsoft.Xna.Framework;
using Strange_Universe.Game.Entities;
using Strange_Universe.Game.NavSystem;
using Strange_Universe.Tests.Fakes;
using Xunit;

namespace Strange_Universe.Tests;

/// <summary>Nav system review step 3: shared steering primitives.</summary>
public class SteeringTests
{
    [Fact]
    public void AllowedApproachSpeed_IsArrivalSpeed_AtTheTarget_AndGrowsWithDistance()
    {
        var ship = new Nonplayer("s");
        float atTarget = Steering.AllowedApproachSpeed(ship, 0f, 100f);
        float near = Steering.AllowedApproachSpeed(ship, 500f, 100f);
        float far = Steering.AllowedApproachSpeed(ship, 5000f, 100f);

        Assert.Equal(100f, atTarget, 3);
        Assert.True(near > atTarget);
        Assert.True(far >= near);
        Assert.Equal(ship.ShipType.MaxSpeed, Steering.AllowedApproachSpeed(ship, 1_000_000f, 100f), 3); // capped
        Assert.Equal(100f, Steering.AllowedApproachSpeed(ship, -50f, 100f), 3); // negative distance behaves like zero
    }

    [Fact]
    public void Arrive_BringsShipToArrivalSpeed_AtTheTarget()
    {
        using var sim = new NavSim();
        var target = new Vector2(6000, 0);
        sim.Place(Vector2.Zero);

        float speedNearTarget = float.NaN;
        sim.Run(120f, () => !float.IsNaN(speedNearTarget), () =>
        {
            float d = Vector2.Distance(sim.Ship.Position, target);
            Steering.Arrive(sim.Ship, target - sim.Ship.Position, d, arrivalSpeed: 50f, NavSim.Dt);
            if (d < 100f) speedNearTarget = sim.Ship.Speed;
        });

        Assert.False(float.IsNaN(speedNearTarget), "ship never reached the target");
        Assert.True(speedNearTarget < 150f, $"arrived at {speedNearTarget:F0}");
    }

    [Fact]
    public void Brake_StopsAMovingShip()
    {
        using var sim = new NavSim();
        sim.Place(Vector2.Zero, new Vector2(500, 0));

        sim.Run(30f, () => sim.Ship.Speed < Steering.StoppedSpeed * 2f, () => Steering.Brake(sim.Ship, NavSim.Dt));

        Assert.True(sim.Ship.Speed < Steering.StoppedSpeed * 2f, $"still moving at {sim.Ship.Speed:F0}");
    }

    [Fact]
    public void AvoidObstacles_NoObstacles_ReturnsNormalizedDirection()
    {
        var ship = new Nonplayer("s");
        var dir = Steering.AvoidObstacles(ship, new FakeWorld(), new Vector2(10, 0));
        Assert.Equal(new Vector2(1, 0), dir);
    }

    [Fact]
    public void AvoidObstacles_BendsAwayFromAPlanetTheShipIsHeadingFor()
    {
        var ship = new Nonplayer("s") { Position = Vector2.Zero, Velocity = new Vector2(500, 0) };
        var world = new FakeWorld();
        world.PlanetList.Add(new Planet("p", 150f, 150f, 1000f, 1000f, 1, 1));
        var planet = world.PlanetList[0];
        planet.Position = new Vector2(500, 10); // close and almost dead ahead

        var dir = Steering.AvoidObstacles(ship, world, new Vector2(1, 0));

        Assert.True(dir.Y < 0f, "should steer away from the planet, which is slightly to the +Y side");
    }
}
