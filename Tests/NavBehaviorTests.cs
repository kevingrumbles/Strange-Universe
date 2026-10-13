using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Strange_Universe.Game.Entities;
using Strange_Universe.Game.NavSystem;
using Strange_Universe.Tests.Fakes;
using Xunit;

namespace Strange_Universe.Tests;

/// <summary>
/// Nav system review: behavior of the autopilot tasks in a simulated system.
/// Skipped tests document known defects from Docs/nav-system-review.md and are un-skipped by the step that fixes them.
/// Gravity is currently disabled (GravityWell.GravityThrustRatio == 0); these tests describe that mode.
/// </summary>
public class NavBehaviorTests
{
    private static float MaxSpeedOf(Ship s) => s.ShipType.MaxSpeed;

    // -- Dock ---------------------------------------------------------------

    [Fact]
    public void Dock_NeverTeleports_AndCompletesNearThePlanet()
    {
        using var sim = new NavSim();
        var planet = sim.System.Planets[^1];
        sim.Place(planet.Position + new Vector2(5000, 0));
        var task = sim.Start(new DockTask(sim.Ship, planet.Position, holdDuration: 20f));

        float largestStep = sim.RunAndMeasureLargestStep(120f, () => task.CurrentState == TaskState.Complete);

        Assert.Equal(TaskState.Complete, task.CurrentState);
        // A ship at top speed moves under 10 units per step; a NaN reset to the origin moves thousands.
        Assert.True(largestStep < 50f, $"ship jumped {largestStep:F0} units in one step");
        Assert.True(Vector2.Distance(sim.Ship.Position, planet.Position) < 1500f,
            "docked ship should still be near the planet");
        Assert.True(float.IsFinite(sim.Ship.Velocity.X) && float.IsFinite(sim.Ship.Velocity.Y));
    }

    [Fact]
    public void Dock_HoldsStation_WithinTolerance()
    {
        using var sim = new NavSim();
        var planet = sim.System.Planets[^1];
        sim.Place(planet.Position + new Vector2(5000, 0));
        var task = sim.Start(new DockTask(sim.Ship, planet.Position, holdDuration: 30f));

        sim.Run(60f, () => task.CurrentState == TaskState.HoldPosition);
        Assert.Equal(TaskState.HoldPosition, task.CurrentState);

        // Let the approach speed bleed off, then the ship must stay put.
        sim.Run(8f);
        Vector2 anchor = sim.Ship.Position;
        float maxDrift = 0f;
        sim.Run(15f, () => task.CurrentState != TaskState.HoldPosition,
            () => maxDrift = System.Math.Max(maxDrift, Vector2.Distance(sim.Ship.Position, anchor)));

        Assert.True(maxDrift < 300f, $"drifted {maxDrift:F0} units while holding");
    }

    // -- Patrol -------------------------------------------------------------

    [Fact]
    public void Patrol_VisitsEveryWaypoint_InOrder()
    {
        using var sim = new NavSim();
        var points = new List<Vector2> { new(2000, 0), new(2000, 2500), new(-1500, 2500) };
        sim.Place(new Vector2(0, 0));
        sim.Start(new PatrolTask(sim.Ship, points));

        var visited = new List<int>();
        sim.Run(300f, () => visited.Count >= 6, () =>
        {
            int next = visited.Count % points.Count;
            if (Vector2.Distance(sim.Ship.Position, points[next]) < 500f) visited.Add(next);
        });

        Assert.Equal(new[] { 0, 1, 2, 0, 1, 2 }, visited);
    }

    [Fact]
    public void Patrol_WithNoWaypoints_Completes()
    {
        using var sim = new NavSim();
        var task = sim.Start(new PatrolTask(sim.Ship, new List<Vector2>()));

        sim.Run(1f, () => task.CurrentState == TaskState.Complete);

        Assert.Equal(TaskState.Complete, task.CurrentState);
    }

    [Fact]
    public void Patrol_SlowsDown_ForAWaypoint()
    {
        using var sim = new NavSim();
        var points = new List<Vector2> { new(6000, 0), new(6000, 6000) };
        sim.Place(Vector2.Zero);
        sim.Start(new PatrolTask(sim.Ship, points));

        float speedAtArrival = float.NaN;
        sim.Run(120f, () => !float.IsNaN(speedAtArrival), () =>
        {
            if (float.IsNaN(speedAtArrival) && Vector2.Distance(sim.Ship.Position, points[0]) < 500f)
                speedAtArrival = sim.Ship.Speed;
        });

        Assert.True(speedAtArrival < MaxSpeedOf(sim.Ship) * 0.6f, $"arrived at {speedAtArrival:F0}");
    }

    // -- Guard --------------------------------------------------------------

    [Fact(Skip = "bug: guard settles at about 3x the requested orbit radius. See nav-system-review finding 2")]
    public void Guard_KeepsRequestedOrbitRadius()
    {
        using var sim = new NavSim();
        var planet = sim.System.Planets[0];
        float orbit = planet.Radius * 2f;
        sim.Place(planet.Position + new Vector2(2000, 0));
        sim.Start(new GuardTask(sim.Ship, planet.Position, orbit));

        sim.Run(40f); // settle
        var radii = new List<float>();
        sim.Run(40f, null, () => radii.Add(Vector2.Distance(sim.Ship.Position, planet.Position)));

        Assert.InRange(radii.Min(), orbit * 0.7f, orbit * 1.3f);
        Assert.InRange(radii.Max(), orbit * 0.7f, orbit * 1.3f);
    }

    [Fact]
    public void Guard_DoesNotDriftAwayFromItsTarget()
    {
        using var sim = new NavSim();
        var planet = sim.System.Planets[0];
        sim.Place(planet.Position + new Vector2(2000, 0));
        sim.Start(new GuardTask(sim.Ship, planet.Position, planet.Radius * 2f));

        float farthest = 0f;
        sim.Run(80f, null, () => farthest = System.Math.Max(farthest, Vector2.Distance(sim.Ship.Position, planet.Position)));

        Assert.True(farthest < 6000f, $"drifted away from the target (farthest {farthest:F0})");
    }

    [Fact(Skip = "bug: guard approaches the target's centre at full speed and passes through the planet. See nav-system-review finding 2")]
    public void Guard_NeverPassesThroughItsTarget()
    {
        using var sim = new NavSim();
        var planet = sim.System.Planets[0];
        sim.Place(planet.Position + new Vector2(2000, 0));
        sim.Start(new GuardTask(sim.Ship, planet.Position, planet.Radius * 2f));

        float closest = float.MaxValue;
        sim.Run(80f, null, () => closest = System.Math.Min(closest, Vector2.Distance(sim.Ship.Position, planet.Position)));

        Assert.True(closest > planet.Radius, $"flew through the planet (closest {closest:F0})");
    }

    [Fact]
    public void Patrol_LongRealisticRoute_KeepsMoving()
    {
        // Waypoints taken from a generated system: far apart, on opposite sides of the star.
        var points = new List<Vector2>
        {
            new(14925, 1549), new(-13155, -13672), new(-14771, 12572), new(-12478, 1834),
        };
        using var sim = new NavSim();
        sim.Place(points[3] + new Vector2(800, 0));
        var task = sim.Start(new PatrolTask(sim.Ship, points));

        int advances = 0, last = task.CurrentWaypointIndex;
        var times = new List<float>();
        sim.Run(600f, () => advances >= 8, () =>
        {
            if (task.CurrentWaypointIndex != last) { advances++; last = task.CurrentWaypointIndex; times.Add(sim.Time); }
        });

        string when = string.Join(", ", times.Select(t => t.ToString("F0")));
        Assert.True(advances >= 8, $"only {advances} waypoints in {sim.Time:F0}s (at {when})");

        // A leg is about 25,000 units at 580 units/s, so roughly 45 s; allow for slowing and turning at each waypoint.
        float previous = 0f;
        foreach (float t in times)
        {
            Assert.True(t - previous < 80f, $"a leg took {t - previous:F0}s (waypoints at {when})");
            previous = t;
        }
    }
}
