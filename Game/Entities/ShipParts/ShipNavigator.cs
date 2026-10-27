using System;
using System.Collections.Generic;
using System.Linq;
using Strange_Universe.Game.NavSystem;

namespace Strange_Universe.Game.Entities.ShipParts;

/// <summary>Nav-task queue: autopilot tasks such as spawn, dock, jump, patrol and guard.</summary>
public class ShipNavigator
{
    private readonly Ship _owner;
    private readonly Queue<NavTask> _queue = new();

    public ShipNavigator(Ship owner)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    public NavTask ActiveTask { get; private set; }

    // Set when the ship was attacked while carrying out a task that must not be suspended; the alert starts once it ends.
    private bool _alertPending;

    /// <summary>True while the ship is reacting to an attack (see <see cref="AlertTask"/>).</summary>
    public bool IsAlerting => ActiveTask is AlertTask;

    /// <summary>
    /// The ship has been attacked and its attacker flagged hostile. Swaps an <see cref="AlertTask"/> in for the active task, which is kept
    /// and resumed when the alert ends. A task that cannot be suspended finishes first. Does nothing if already alerting
    /// (the new hostile is picked up by the running alert).
    /// </summary>
    public void RaiseAlert()
    {
        if (ActiveTask is AlertTask)
            return;

        if (ActiveTask is { CanBeInterrupted: false })
        {
            _alertPending = true;
            return;
        }

        ActiveTask = new AlertTask(_owner, ActiveTask);
    }

    public bool HasActiveTask => ActiveTask is not null;

    /// <summary>True if a task of type <typeparamref name="T"/> is active or waiting.</summary>
    public bool Has<T>() where T : NavTask => ActiveTask is T || _queue.OfType<T>().Any();

    /// <summary>Number of tasks waiting behind the active one.</summary>
    public int QueuedCount => _queue.Count;

    /// <summary>Queues <paramref name="task"/>. It must belong to this navigator's ship.</summary>
    public void Enqueue(NavTask task)
    {
        ArgumentNullException.ThrowIfNull(task);
        if (!ReferenceEquals(task.Owner, _owner))
            throw new ArgumentException("A nav task can only be queued on the ship it belongs to.", nameof(task));

        _queue.Enqueue(task);
    }

    /// <summary>Drops the active task and everything queued behind it. No completion or failure hooks run.</summary>
    public void Clear()
    {
        ActiveTask = null;
        _alertPending = false;
        _queue.Clear();
    }

    /// <summary>Advances the active nav task and starts the next queued one when it finishes.</summary>
    public void Update(float deltaTime)
    {
        NavTask finished = null;

        if (ActiveTask is not null)
        {
            if (!ActiveTask.IsFinished)
                ActiveTask.Update(deltaTime);
            switch (ActiveTask.Status)
            {
                case NavTaskStatus.Complete:
                    ActiveTask.OnCompleted();
                    finished = ActiveTask;
                    ActiveTask = null;
                    break;
                case NavTaskStatus.Invalid:
                    ActiveTask.OnFailed();
                    finished = ActiveTask;
                    ActiveTask = null;
                    break;
            }
        }

        if (ActiveTask is not null)
            return;

        if (finished is AlertTask { Previous: { IsFinished: false } interrupted })
        {
            // The alert is over: carry on with what the ship was doing, where it left off.
            ActiveTask = interrupted;
        }
        else if (_alertPending && _owner.Hostiles.Count > 0)
        {
            // Attacked during a task that could not be suspended; that task is done, so respond now.
            _alertPending = false;
            ActiveTask = new AlertTask(_owner, null);
        }
        else if (_queue.Count > 0)
        {
            _alertPending = false;
            ActiveTask = _queue.Dequeue();
        }
    }
}
