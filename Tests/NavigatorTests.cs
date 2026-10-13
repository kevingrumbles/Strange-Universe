using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Strange_Universe.Game.Components;
using Strange_Universe.Game.Entities;
using Strange_Universe.Game.NavSystem;
using Strange_Universe.Tests.Fakes;
using Xunit;

namespace Strange_Universe.Tests;

/// <summary>Nav system review step 4: navigator control, task hooks, and manual input while the autopilot flies.</summary>
public class NavigatorTests
{
    /// <summary>A task whose state the test controls, counting the navigator's hook calls.</summary>
    private sealed class ProbeTask : NavTask
    {
        public int Updates, Completed, Failed;
        public ProbeTask(Ship owner, TaskState state = TaskState.Patrolling) : base(owner) => CurrentState = state;
        public override void Update(float deltaTime) => Updates++;
        public override void OnCompleted() => Completed++;
        public override void OnFailed() => Failed++;
    }

    private static Nonplayer Ship() => new("nav-test");

    /// <summary>One navigator update: starts the next task when none is active.</summary>
    private static void Step(Ship ship) => ship.Navigator.Update(0.016f);

    // -- queue control ------------------------------------------------------

    [Fact]
    public void Enqueue_RejectsNullAndForeignTasks()
    {
        var ship = Ship();
        var other = Ship();

        int before = ship.Navigator.QueuedCount; // a new NPC already has its spawn task queued
        Assert.Throws<ArgumentNullException>(() => ship.Navigator.Enqueue(null));
        Assert.Throws<ArgumentException>(() => ship.Navigator.Enqueue(new ProbeTask(other)));
        Assert.Equal(before, ship.Navigator.QueuedCount);
    }

    [Fact]
    public void Tasks_RunInOrder_OneAtATime()
    {
        var ship = Ship();
        ship.Navigator.Clear();
        var first = new ProbeTask(ship);
        var second = new ProbeTask(ship);
        ship.Navigator.Enqueue(first);
        ship.Navigator.Enqueue(second);

        Step(ship);                      // starts first
        Assert.Same(first, ship.Navigator.ActiveTask);
        Assert.Equal(1, ship.Navigator.QueuedCount);

        first.CurrentState = TaskState.Complete;
        Step(ship);                      // first finishes, second starts
        Assert.Same(second, ship.Navigator.ActiveTask);
        Assert.Equal(0, ship.Navigator.QueuedCount);
    }

    [Fact]
    public void CancelActive_DropsOnlyTheActiveTask_WithoutHooks()
    {
        var ship = Ship();
        ship.Navigator.Clear();
        var first = new ProbeTask(ship);
        var second = new ProbeTask(ship);
        ship.Navigator.Enqueue(first);
        ship.Navigator.Enqueue(second);
        Step(ship);

        ship.Navigator.CancelActive();

        Assert.False(ship.Navigator.HasActiveTask);
        Assert.Equal(1, ship.Navigator.QueuedCount);
        Step(ship);
        Assert.Same(second, ship.Navigator.ActiveTask);
        Assert.Equal((0, 0), (first.Completed, first.Failed));
    }

    [Fact]
    public void Clear_DropsActiveAndQueued_WithoutHooks()
    {
        var ship = Ship();
        ship.Navigator.Clear();
        var first = new ProbeTask(ship);
        var second = new ProbeTask(ship);
        ship.Navigator.Enqueue(first);
        ship.Navigator.Enqueue(second);
        Step(ship);

        ship.Navigator.Clear();
        Step(ship);

        Assert.False(ship.Navigator.HasActiveTask);
        Assert.Equal(0, ship.Navigator.QueuedCount);
        Assert.Equal((0, 0, 0, 0), (first.Completed, first.Failed, second.Completed, second.Failed));
    }

    // -- hooks --------------------------------------------------------------

    [Fact]
    public void CompletedTask_GetsOnCompletedOnce_NotOnFailed()
    {
        var ship = Ship();
        ship.Navigator.Clear();
        var task = new ProbeTask(ship);
        ship.Navigator.Enqueue(task);
        Step(ship);

        task.CurrentState = TaskState.Complete;
        Step(ship);
        Step(ship);

        Assert.Equal((1, 0), (task.Completed, task.Failed));
        Assert.False(ship.Navigator.HasActiveTask);
    }

    [Fact]
    public void InvalidTask_GetsOnFailedOnce_NotOnCompleted()
    {
        var ship = Ship();
        ship.Navigator.Clear();
        var task = new ProbeTask(ship);
        ship.Navigator.Enqueue(task);
        Step(ship);

        task.CurrentState = TaskState.Invalid;
        Step(ship);
        Step(ship);

        Assert.Equal((0, 1), (task.Completed, task.Failed));
        Assert.False(ship.Navigator.HasActiveTask);
    }

    [Fact]
    public void InvalidTask_DoesNotStopTheQueue()
    {
        var ship = Ship();
        ship.Navigator.Clear();
        var bad = new ProbeTask(ship, TaskState.Invalid);
        var good = new ProbeTask(ship);
        ship.Navigator.Enqueue(bad);
        ship.Navigator.Enqueue(good);

        Step(ship); // starts bad
        Step(ship); // bad is invalid -> failed, good starts

        Assert.Equal(1, bad.Failed);
        Assert.Same(good, ship.Navigator.ActiveTask);
    }

    // -- player input while the autopilot flies -----------------------------

    private static Universe StartedUniverse()
    {
        var u = new Universe("Input", "input-seed") { NebulaFactory = Nebula.CreateWithoutPixels };
        u.Generate(new RecordingAssetRequests());
        // Let the opening spawn task finish so the test controls what is active.
        for (int i = 0; i < 5; i++) u.Player.Update(0.016f, new InputState());
        return u;
    }

    private static InputState AllFlightKeys() => new()
    {
        RotateLeft = true, Thrust = true, Retrograde = true, Jump = true,
    };

    [Fact]
    public void Player_FlightControls_AreIgnored_WhileAnAutopilotTaskIsActive()
    {
        var u = StartedUniverse();
        var player = u.Player;
        player.Position = Vector2.Zero;
        player.Velocity = new Vector2(100, 0);
        player.Rotation = 0f;
        u.JumpRoute.Add(u.ActiveStarSystem.SystemConnectionIds.First()); // a held J must not queue a second jump
        player.EnqueueNavTask(new ProbeTask(player));
        player.Update(0.016f, new InputState()); // task becomes active
        Assert.True(player.HasActiveNavTask);

        for (int i = 0; i < 60; i++) player.Update(0.016f, AllFlightKeys());

        Assert.Equal(0f, player.Rotation);          // no rotation from the keys
        Assert.Equal(new Vector2(100, 0), player.Velocity); // no thrust from the keys
        Assert.Equal(0, player.Navigator.QueuedCount);      // J did not queue another jump
        u.Dispose();
    }

    [Fact]
    public void Player_FlightControls_Work_WhenNoTaskIsActive()
    {
        var u = StartedUniverse();
        var player = u.Player;
        Assert.False(player.HasActiveNavTask);
        player.Rotation = 0f;
        player.Velocity = Vector2.Zero;

        for (int i = 0; i < 30; i++) player.Update(0.016f, new InputState { RotateLeft = true, Thrust = true });

        Assert.NotEqual(0f, player.Rotation);
        Assert.True(player.Speed > 0f);
        u.Dispose();
    }
}
