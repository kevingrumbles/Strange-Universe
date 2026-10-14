using System;
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
    private static float MaxSpeedOf(Ship s) => s.ShipType.Flight.MaxSpeed;

    // -- Dock ---------------------------------------------------------------

    [Fact]
    public void Dock_NeverTeleports_AndCompletesNearThePlanet()
    {
        using var sim = new NavSim();
        var planet = sim.System.Planets[^1];
        sim.Place(planet.Position + new Vector2(5000, 0));
        var task = sim.Start(new DockTask(sim.Ship, planet.Position, holdDuration: 20f));

        float largestStep = sim.RunAndMeasureLargestStep(sim.TravelTime(5000f) + 20f, () => task.Status == NavTaskStatus.Complete);

        Assert.Equal(NavTaskStatus.Complete, task.Status);
        // A ship moves at most its top speed times the step; a NaN reset to the origin moves thousands.
        Assert.True(largestStep < sim.MaxSpeed * NavSim.Dt * 3f + 1f, $"ship jumped {largestStep:F0} units in one step");
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

        sim.Run(sim.TravelTime(5000f), () => task.CurrentPhase == DockTask.Phase.Hold);
        Assert.Equal(DockTask.Phase.Hold, task.CurrentPhase);

        // Let the approach speed bleed off, then the ship must stay put.
        sim.Run(8f);
        Vector2 anchor = sim.Ship.Position;
        float maxDrift = 0f;
        sim.Run(15f, () => task.CurrentPhase != DockTask.Phase.Hold,
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
        sim.Run(sim.TravelTime(2500f) * 6f, () => visited.Count >= 6, () =>
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

        sim.Run(1f, () => task.Status == NavTaskStatus.Complete);

        Assert.Equal(NavTaskStatus.Complete, task.Status);
    }

    [Fact]
    public void Patrol_SlowsDown_ForAWaypoint()
    {
        using var sim = new NavSim();
        var points = new List<Vector2> { new(6000, 0), new(6000, 6000) };
        sim.Place(Vector2.Zero);
        sim.Start(new PatrolTask(sim.Ship, points));

        float speedAtArrival = float.NaN;
        sim.Run(sim.TravelTime(6000f), () => !float.IsNaN(speedAtArrival), () =>
        {
            if (float.IsNaN(speedAtArrival) && Vector2.Distance(sim.Ship.Position, points[0]) < 500f)
                speedAtArrival = sim.Ship.Speed;
        });

        Assert.True(speedAtArrival < MaxSpeedOf(sim.Ship) * 0.6f, $"arrived at {speedAtArrival:F0}");
    }

    // -- Guard --------------------------------------------------------------

    [Fact]
    public void Guard_KeepsRequestedOrbitRadius()
    {
        using var sim = new NavSim();
        var planet = sim.System.Planets[0];
        float orbit = planet.Radius * 2f;
        sim.Place(planet.Position + new Vector2(2000, 0));
        sim.Start(new GuardTask(sim.Ship, planet.Position, orbit));

        sim.Run(sim.TravelTime(2000f)); // settle
        var radii = new List<float>();
        sim.Run(40f, null, () => radii.Add(Vector2.Distance(sim.Ship.Position, planet.Position)));

        Assert.InRange(radii.Min(), orbit * 0.9f, orbit * 1.1f);
        Assert.InRange(radii.Max(), orbit * 0.9f, orbit * 1.1f);
    }

    [Fact]
    public void Guard_DoesNotDriftAwayFromItsTarget()
    {
        using var sim = new NavSim();
        var planet = sim.System.Planets[0];
        sim.Place(planet.Position + new Vector2(2000, 0));
        sim.Start(new GuardTask(sim.Ship, planet.Position, planet.Radius * 2f));

        float farthest = 0f;
        sim.Run(sim.TravelTime(2000f) + 50f, null, () => farthest = System.Math.Max(farthest, Vector2.Distance(sim.Ship.Position, planet.Position)));

        Assert.True(farthest < 6000f, $"drifted away from the target (farthest {farthest:F0})");
    }

    [Fact]
    public void Guard_NeverPassesThroughItsTarget()
    {
        using var sim = new NavSim();
        var planet = sim.System.Planets[0];
        sim.Place(planet.Position + new Vector2(2000, 0));
        sim.Start(new GuardTask(sim.Ship, planet.Position, planet.Radius * 2f));

        float closest = float.MaxValue;
        sim.Run(sim.TravelTime(2000f) + 50f, null, () => closest = System.Math.Min(closest, Vector2.Distance(sim.Ship.Position, planet.Position)));

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
        sim.Run(sim.TravelTime(32000f) * 8f, () => advances >= 8, () =>
        {
            if (task.CurrentWaypointIndex != last) { advances++; last = task.CurrentWaypointIndex; times.Add(sim.Time); }
        });

        string when = string.Join(", ", times.Select(t => t.ToString("F0")));
        Assert.True(advances >= 8, $"only {advances} waypoints in {sim.Time:F0}s (at {when})");

        // Each leg should take about its length divided by the ship's top speed, with some allowance for slowing and turning
        // at the waypoint. A ship that loiters around a waypoint (an old bug) takes far longer.
        Vector2 start = points[3] + new Vector2(800, 0);
        float LegLength(int leg) => leg == 0 ? Vector2.Distance(start, points[0]) : Vector2.Distance(points[(leg - 1) % 4], points[leg % 4]);
        int legIndex = 0;
        float previous = 0f;
        foreach (float t in times)
        {
            float limit = LegLength(legIndex) / sim.MaxSpeed * 1.3f + 25f;
            Assert.True(t - previous < limit, $"leg {legIndex} took {t - previous:F0}s, limit {limit:F0}s (waypoints at {when})");
            legIndex++;
            previous = t;
        }
    }

    [Theory]
    [InlineData(2000f, 0f)]      // near, outside the ring
    [InlineData(-9000f, 300f)]   // far across the system
    [InlineData(80f, 0f)]        // starting inside the ring
    [InlineData(0f, 437f)]       // starting on the ring
    public void Guard_SettlesIntoASteadyOrbit_FromAnyStart(float dx, float dy)
    {
        using var sim = new NavSim();
        var planet = sim.System.Planets[0];
        float orbit = planet.Radius * 2f;
        sim.Place(planet.Position + new Vector2(dx, dy));
        var task = sim.Start(new GuardTask(sim.Ship, planet.Position, orbit));

        sim.Run(sim.TravelTime(new Vector2(dx, dy).Length())); // settle
        float minRadius = float.MaxValue, maxRadius = 0f, minSpeed = float.MaxValue, maxSpeed = 0f;
        sim.Run(30f, null, () =>
        {
            float d = Vector2.Distance(sim.Ship.Position, planet.Position);
            minRadius = System.Math.Min(minRadius, d); maxRadius = System.Math.Max(maxRadius, d);
            minSpeed = System.Math.Min(minSpeed, sim.Ship.Speed); maxSpeed = System.Math.Max(maxSpeed, sim.Ship.Speed);
        });

        Assert.InRange(minRadius, orbit * 0.9f, orbit * 1.1f);
        Assert.InRange(maxRadius, orbit * 0.9f, orbit * 1.1f);
        float orbitSpeed = task.OrbitSpeedFor(sim.Ship);
        Assert.InRange(minSpeed, orbitSpeed * 0.9f, orbitSpeed * 1.1f);
        Assert.InRange(maxSpeed, orbitSpeed * 0.9f, orbitSpeed * 1.1f);
    }

    [Fact]
    public void Guard_OrbitSpeed_IsWithinWhatTheEngineCanTurn()
    {
        var ship = new Nonplayer("g");
        foreach (float radius in new[] { 50f, 200f, 437f, 2000f })
        {
            var task = new GuardTask(ship, Vector2.Zero, radius);
            float v = task.OrbitSpeedFor(ship);

            Assert.True(v <= ship.ShipType.Flight.MaxSpeed * GuardTask.OrbitSpeedFraction + 0.01f);
            Assert.True(v * v / radius <= ship.MaxAcceleration * GuardTask.CentripetalBudget + 0.01f,
                $"radius {radius}: needs {v * v / radius:F0} of {ship.MaxAcceleration:F0}");
        }
    }

    [Theory]
    [InlineData(5000f, 0f)]       // straight in from the east
    [InlineData(-6000f, 6000f)]   // far, from the north-west
    [InlineData(700f, 0f)]        // starting close
    [InlineData(0f, -1200f)]      // starting just outside the hold distance
    public void Dock_StopsAtTheHoldPoint_WithoutOvershooting(float dx, float dy)
    {
        using var sim = new NavSim();
        var planet = sim.System.Planets[^1];
        sim.Place(planet.Position + new Vector2(dx, dy));
        var task = sim.Start(new DockTask(sim.Ship, planet.Position, holdDuration: 25f));

        float closest = float.MaxValue, maxDrift = 0f, speedAtHold = float.NaN, holdStart = float.NaN, doneAt = float.NaN;
        sim.Run(sim.TravelTime(new Vector2(dx, dy).Length()) + 40f, () => !float.IsNaN(doneAt), () =>
        {
            closest = System.Math.Min(closest, Vector2.Distance(sim.Ship.Position, planet.Position));
            if (task.CurrentPhase == DockTask.Phase.Hold)
            {
                if (float.IsNaN(holdStart)) { holdStart = sim.Time; speedAtHold = sim.Ship.Speed; }
                maxDrift = System.Math.Max(maxDrift, Vector2.Distance(sim.Ship.Position, task.HoldPoint.Value));
            }
            if (task.Status == NavTaskStatus.Complete && float.IsNaN(doneAt)) doneAt = sim.Time;
        });

        Assert.False(float.IsNaN(doneAt), "dock never completed");
        // It slows in time: never gets much closer than the hold distance, and is nearly stopped on arrival.
        Assert.True(closest > DockTask.SafeDistance * 0.9f, $"came within {closest:F0} of the target");
        Assert.True(speedAtHold <= sim.Ship.ShipType.Flight.MaxSpeed * DockTask.HoldArrivalSpeedFraction + 1f, $"arrived at speed {speedAtHold:F0}");
        Assert.True(maxDrift < DockTask.HoldArrivalDistance, $"drifted {maxDrift:F0} from the hold point while holding");
        // The hold lasts as long as asked.
        Assert.InRange(doneAt - holdStart, 24.9f, 25.2f);
    }

    [Fact]
    public void Dock_HoldPoint_IsOnTheSideTheShipArrivesFrom()
    {
        using var sim = new NavSim();
        var planet = sim.System.Planets[^1];
        sim.Place(planet.Position + new Vector2(0, 3000));
        var task = sim.Start(new DockTask(sim.Ship, planet.Position, 10f));

        sim.Run(1f);

        var hold = task.HoldPoint.Value;
        Assert.Equal(DockTask.SafeDistance, Vector2.Distance(hold, planet.Position), 1);
        Assert.Equal(planet.Position.X, hold.X, 1);
        Assert.True(hold.Y > planet.Position.Y);
    }

    [Fact]
    public void Dock_DefaultHoldDuration_IsInRange_AndReproducible()
    {
        Nonplayer Ship(string id) => new(id);
        var a1 = new DockTask(Ship("Npc_merchant_1"), Vector2.Zero);
        var a2 = new DockTask(Ship("Npc_merchant_1"), Vector2.Zero);
        var b = new DockTask(Ship("Npc_merchant_2"), Vector2.Zero);

        Assert.Equal(a1.HoldDuration, a2.HoldDuration);
        Assert.NotEqual(a1.HoldDuration, b.HoldDuration);
        foreach (var t in new[] { a1, b })
            Assert.InRange(t.HoldDuration, DockTask.MinHoldSeconds, DockTask.MaxHoldSeconds);
    }

    // -- Jump (pre-jump phases) ---------------------------------------------

    private static readonly JumpTask.Phase[] PreJumpOrder =
        { JumpTask.Phase.MoveToMandeville, JumpTask.Phase.Decelerate, JumpTask.Phase.AlignForJump, JumpTask.Phase.Jump };

    [Theory]
    // Velocities are multiples of the ship's top speed, so the cases keep their meaning when the ship is retuned.
    [InlineData(1000f, 0f, 0f, 0f)]          // at rest, inside Mandeville
    [InlineData(4000f, 0f, 0f, 1f)]          // flying sideways at full speed
    [InlineData(14000f, 0f, -1f, 0f)]        // flying inward at full speed
    [InlineData(0f, 0f, 0f, 0f)]             // exactly at the center
    public void Jump_RunsThePhasesInOrder_AndLeavesTheSystem(float x, float y, float vx, float vy)
    {
        using var sim = new NavSim();
        sim.Place(new Vector2(x, y), new Vector2(vx, vy) * sim.MaxSpeed);
        var task = sim.Start(new JumpTask(sim.Ship));

        var seen = new List<JumpTask.Phase>();
        float speedWhenJumpStarted = float.NaN;
        sim.Run(sim.TravelTime(sim.System.SystemRadius * 1.5f), () => sim.Ship.Remove, () =>
        {
            if (seen.Count == 0 || seen[^1] != task.CurrentPhase)
            {
                seen.Add(task.CurrentPhase);
                if (task.CurrentPhase == JumpTask.Phase.Jump) speedWhenJumpStarted = sim.Ship.Speed;
            }
        });

        Assert.True(sim.Ship.Remove, "ship never left the system");
        Assert.Equal(PreJumpOrder, seen);
        Assert.Equal(NavTaskStatus.Complete, task.Status);
        Assert.True(speedWhenJumpStarted <= PrepareJumpTask.SafeJumpSpeed + 1f, $"started the jump at speed {speedWhenJumpStarted:F0}");
    }

    [Fact]
    public void Jump_LeavesAlongItsFacing_NotAlongLeftoverDrift()
    {
        // Outside the Mandeville radius and drifting inward slowly: the jump used to follow the drift,
        // sending the ship backwards through the system.
        using var sim = new NavSim();
        float drift = System.Math.Min(PrepareJumpTask.SafeJumpSpeed, sim.MaxSpeed) * 0.9f; // slow enough that the ship does not brake first
        sim.Place(new Vector2(16000, 0), new Vector2(-drift, 0));
        var task = sim.Start(new JumpTask(sim.Ship));

        float worstAngle = 0f;
        sim.Run(60f, () => sim.Ship.Remove, () =>
        {
            // Only once the drive has clearly overcome the leftover drift (which is not a heading in itself).
            if (task.CurrentPhase == JumpTask.Phase.Jump && sim.Ship.Speed > System.Math.Max(drift * 4f, sim.MaxSpeed * 0.5f))
            {
                float cos = Vector2.Dot(Vector2.Normalize(sim.Ship.Velocity), sim.Ship.Forward);
                worstAngle = System.Math.Max(worstAngle, MathHelper.ToDegrees(MathF.Acos(MathHelper.Clamp(cos, -1f, 1f))));
            }
        });

        Assert.True(sim.Ship.Remove);
        Assert.True(sim.Time < 6f, $"took {sim.Time:F1}s");
        Assert.True(worstAngle < 10f, $"travelled {worstAngle:F0} degrees off its facing");
        Assert.True(sim.Ship.Position.X > sim.System.SystemRadius * 0.97f, "should leave outward, on the side it started");
    }

    [Fact]
    public void Jump_FromASidewaysStart_StillGoesOutward()
    {
        using var sim = new NavSim();
        sim.Place(new Vector2(4000, 0), new Vector2(0, sim.MaxSpeed)); // sideways at full speed
        sim.Start(new JumpTask(sim.Ship));

        sim.Run(sim.TravelTime(sim.System.SystemRadius * 1.5f), () => sim.Ship.Remove);

        Assert.True(sim.Ship.Remove);
        Assert.True(sim.Time < sim.TravelTime(sim.System.SystemRadius), $"took {sim.Time:F1}s");
        // Leaves roughly radially from where it left the Mandeville region, not on a long chord.
        float cos = Vector2.Dot(Vector2.Normalize(sim.Ship.Position), Vector2.Normalize(sim.Ship.Velocity));
        Assert.True(cos > 0.99f, $"left {MathHelper.ToDegrees(MathF.Acos(cos)):F0} degrees off radial");
    }
}
