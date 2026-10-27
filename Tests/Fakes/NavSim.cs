using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Strange_Universe.Game.Entities;
using Strange_Universe.Game.Entities.ShipParts;
using Strange_Universe.Game.NavSystem;

namespace Strange_Universe.Tests.Fakes;

/// <summary>
/// Headless nav simulation: a real generated system, one NPC and a fixed 60 Hz step.
/// Lets tests assert on where a ship ends up, how fast it is going and when a task finishes.
/// </summary>
public sealed class NavSim : IDisposable
{
    public const float Dt = 1f / 60f;

    public Universe Universe { get; }
    public StarSystem System { get; }
    public Nonplayer Ship { get; }

    /// <summary>Where the ship's spawn task put it.</summary>
    public Vector2 SpawnPosition { get; }

    /// <summary>The ship's top speed, from its ship stats. Tests must derive speeds and time limits from this, not hard-code them.</summary>
    public float MaxSpeed => Ship.ShipType.Flight.MaxSpeed;

    /// <summary>
    /// A generous time limit for the ship to cover <paramref name="distance"/> at its top speed (50% slack plus a fixed allowance
    /// for turning, braking and settling), so a slower or faster ship gets a proportionate limit.
    /// </summary>
    public float TravelTime(float distance) => distance / MaxSpeed * 1.5f + 30f;

    /// <summary>Total simulated seconds.</summary>
    public float Time { get; private set; }

    public NavSim(string seed = "sim-seed", string shipId = "sim")
    {
        Universe = new Universe("NavSim", seed);
        Universe.Generate(null);
        System = Universe.ActiveStarSystem;

        // A gun of the test's own, so the ship's behavior does not depend on the ship type's default loadout or the catalog's numbers.
        Ship = TestGear.Arm(new Nonplayer(shipId), TestGear.Gun());
        System.AddNpc(Ship);
        // Let the spawn task finish, then park the ship where the test wants it.
        for (int i = 0; i < 3; i++) Ship.Navigator.Update(Dt);
        SpawnPosition = Ship.Position;
    }

    public NavSim Place(Vector2 position, Vector2? velocity = null)
    {
        Ship.Position = position;
        Ship.Velocity = velocity ?? Vector2.Zero;
        return this;
    }

    /// <summary>Queues <paramref name="task"/> on the ship.</summary>
    public T Start<T>(T task) where T : NavTask
    {
        Ship.EnqueueNavTask(task);
        return task;
    }

    /// <summary>Steps until <paramref name="until"/> is true or <paramref name="maxSeconds"/> pass. Calls <paramref name="onStep"/> after every step.</summary>
    public bool Run(float maxSeconds, Func<bool> until = null, Action onStep = null)
    {
        float end = Time + maxSeconds;
        while (Time < end)
        {
            Ship.Update(Dt);
            Time += Dt;
            onStep?.Invoke();
            if (until != null && until()) return true;
        }
        return false;
    }

    /// <summary>The distance the ship moved in the largest single step so far while running <paramref name="seconds"/>.</summary>
    public float RunAndMeasureLargestStep(float seconds, Func<bool> until = null)
    {
        float largest = 0f;
        Vector2 last = Ship.Position;
        Run(seconds, until, () =>
        {
            largest = Math.Max(largest, Vector2.Distance(last, Ship.Position));
            last = Ship.Position;
        });
        return largest;
    }

    /// <summary>
    /// Steps the whole system (player, NPCs, projectiles, collisions) instead of only the ship, so shots fly and hit.
    /// <paramref name="onStep"/> runs after every step; <paramref name="until"/> stops the run early.
    /// </summary>
    public bool RunSystem(float maxSeconds, Func<bool> until = null, Action onStep = null)
    {
        var input = new Strange_Universe.Game.Components.InputState();
        float end = Time + maxSeconds;
        while (Time < end)
        {
            System.Update(Dt, input);
            Time += Dt;
            onStep?.Invoke();
            if (until != null && until()) return true;
        }
        return false;
    }

    public void Dispose() => Universe.Dispose();
}
