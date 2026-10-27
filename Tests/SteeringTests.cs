using Microsoft.Xna.Framework;
using Strange_Universe.Game.Entities;
using Strange_Universe.Game.Entities.ShipParts;
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
        float arrival = ship.ShipType.Flight.MaxSpeed * 0.2f; // an arrival speed well below top speed, whatever the tuning
        float atTarget = Steering.AllowedApproachSpeed(ship, 0f, arrival);
        float near = Steering.AllowedApproachSpeed(ship, 500f, arrival);
        float far = Steering.AllowedApproachSpeed(ship, 5000f, arrival);

        Assert.Equal(arrival, atTarget, 3);
        Assert.True(near > atTarget);
        Assert.True(far >= near);
        Assert.Equal(ship.ShipType.Flight.MaxSpeed, Steering.AllowedApproachSpeed(ship, 1_000_000f, arrival), 3); // capped
        Assert.Equal(arrival, Steering.AllowedApproachSpeed(ship, -50f, arrival), 3); // negative distance behaves like zero
    }

    [Fact]
    public void Arrive_BringsShipToArrivalSpeed_AtTheTarget()
    {
        using var sim = new NavSim();
        var target = new Vector2(6000, 0);
        sim.Place(Vector2.Zero);
        float arrival = sim.MaxSpeed * 0.1f;

        // The ship only has to be at the arrival speed when it reaches the target, so measure at the point of closest approach.
        float closest = float.MaxValue, speedAtClosest = float.NaN;
        sim.Run(sim.TravelTime(6000f), () => sim.Ship.Position.X > 0 && Vector2.Distance(sim.Ship.Position, target) > closest + 100f, () =>
        {
            float d = Vector2.Distance(sim.Ship.Position, target);
            Steering.Arrive(sim.Ship, target - sim.Ship.Position, d, arrivalSpeed: arrival, NavSim.Dt);
            if (d < closest) { closest = d; speedAtClosest = sim.Ship.Speed; }
        });

        Assert.False(float.IsNaN(speedAtClosest), "ship never reached the target");
        Assert.True(closest < 150f, $"closest approach {closest:F0}");
        // Within a few velocity tolerances of the requested speed (the tolerance is a fraction of top speed).
        float allowed = arrival + sim.MaxSpeed * Steering.VelocityTolerance * 3f;
        Assert.True(speedAtClosest < allowed, $"arrived at {speedAtClosest:F0}, allowed {allowed:F0}");
    }

    [Fact]
    public void Brake_StopsAMovingShip()
    {
        using var sim = new NavSim();
        sim.Place(Vector2.Zero, new Vector2(500, 0));

        sim.Run(30f, () => sim.Ship.Speed < Steering.StoppedSpeed * 2f, () => Steering.Brake(sim.Ship, NavSim.Dt));

        Assert.True(sim.Ship.Speed < Steering.StoppedSpeed * 2f, $"still moving at {sim.Ship.Speed:F0}");
    }

}
