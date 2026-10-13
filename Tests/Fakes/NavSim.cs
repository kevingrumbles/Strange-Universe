using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Strange_Universe.Game.Entities;
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

    /// <summary>Total simulated seconds.</summary>
    public float Time { get; private set; }

    public NavSim(string seed = "sim-seed")
    {
        Universe = new Universe("NavSim", seed) { NebulaFactory = Nebula.CreateWithoutPixels };
        Universe.Generate(new RecordingAssetRequests());
        System = Universe.ActiveStarSystem;

        Ship = new Nonplayer("sim");
        System.AddNpc(Ship);
        // Let the spawn task finish, then park the ship where the test wants it.
        for (int i = 0; i < 3; i++) Ship.Navigator.Update(Dt);
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

    public void Dispose() => Universe.Dispose();
}
