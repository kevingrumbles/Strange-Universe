using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Strange_Universe.Game.Components;
using Strange_Universe.Game.Components.ShipComponents;
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
        public ProbeTask(Ship owner, bool failed = false) : base(owner) { if (failed) Fail(); }
        public void Finish() => Complete();
        public void Break() => Fail();
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

        first.Finish();
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

        task.Finish();
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

        task.Break();
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
        var bad = new ProbeTask(ship, failed: true);
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

    // -- status and phases (step 5) ----------------------------------------

    [Fact]
    public void Status_CanOnlyBeChangedByTheTaskItself()
    {
        var status = typeof(NavTask).GetProperty(nameof(NavTask.Status));

        Assert.NotNull(status);
        Assert.True(status.GetMethod.IsPublic);
        Assert.False(status.SetMethod?.IsPublic ?? false, "Status must not be settable from outside the task");
        Assert.Null(typeof(NavTask).GetProperty("CurrentState")); // the shared state enum is gone
    }

    [Fact]
    public void FinishedStatus_IsFinal()
    {
        var ship = Ship();
        var done = new ProbeTask(ship);
        done.Finish();
        done.Break();
        Assert.Equal(NavTaskStatus.Complete, done.Status);

        var failed = new ProbeTask(ship, failed: true);
        failed.Finish();
        Assert.Equal(NavTaskStatus.Invalid, failed.Status);
        Assert.True(failed.IsFinished);
    }

    [Fact]
    public void NavigatorDoesNotUpdateAFinishedTask()
    {
        var ship = Ship();
        ship.Navigator.Clear();
        var task = new ProbeTask(ship, failed: true);
        ship.Navigator.Enqueue(task);

        Step(ship); // starts it
        Step(ship); // sees it failed

        Assert.Equal(0, task.Updates);
        Assert.Equal(1, task.Failed);
    }

    [Fact]
    public void RunningTask_IsUpdatedEveryFrame()
    {
        var ship = Ship();
        ship.Navigator.Clear();
        var task = new ProbeTask(ship);
        ship.Navigator.Enqueue(task);

        Step(ship);
        Step(ship);
        Step(ship);

        Assert.Equal(2, task.Updates); // the first step only starts the task
        Assert.Equal(NavTaskStatus.Running, task.Status);
    }

    [Fact]
    public void PlayerJump_ToAnUnknownTarget_FailsOnItsFirstUpdate()
    {
        var world = new FakeWorld();
        var player = new Player("p", new Shuttle());
        var task = new JumpTask(player, null, "no-such-system") { World = world };

        Assert.Equal(NavTaskStatus.Running, task.Status); // nothing is checked until it runs
        task.Update(0.016f);

        Assert.Equal(NavTaskStatus.Invalid, task.Status);
    }

    [Fact]
    public void PlayerJump_WithoutATarget_FailsOnItsFirstUpdate()
    {
        var task = new JumpTask(new Player("p", new Shuttle()), null, null) { World = new FakeWorld() };
        task.Update(0.016f);
        Assert.Equal(NavTaskStatus.Invalid, task.Status);
    }

    [Fact]
    public void NpcJump_NeedsNoTarget()
    {
        var npc = Ship();
        var task = new JumpTask(npc, null, null) { World = new FakeWorld() };
        task.Update(0.016f);
        Assert.Equal(NavTaskStatus.Running, task.Status);
    }

    [Fact]
    public void Tasks_StartInTheirFirstPhase()
    {
        var ship = Ship();
        Assert.Equal(JumpTask.Phase.MoveToMandeville, new JumpTask(ship).CurrentPhase);
        Assert.Equal(DockTask.Phase.Approach, new DockTask(ship, Vector2.Zero).CurrentPhase);
        Assert.Equal(GuardTask.Phase.Approach, new GuardTask(ship, Vector2.Zero).CurrentPhase);
    }
}
