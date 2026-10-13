using System;
using System.Collections.Generic;
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

    public bool HasActiveTask => ActiveTask is not null;

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

    /// <summary>Drops the active task without completing it; the next queued task starts on the next update.</summary>
    public void CancelActive() => ActiveTask = null;

    /// <summary>Drops the active task and everything queued behind it. No completion or failure hooks run.</summary>
    public void Clear()
    {
        ActiveTask = null;
        _queue.Clear();
    }

    /// <summary>Advances the active nav task and starts the next queued one when it finishes.</summary>
    public void Update(float deltaTime)
    {
        if (ActiveTask is not null)
        {
            ActiveTask.Update(deltaTime);
            switch (ActiveTask.CurrentState)
            {
                case TaskState.Complete:
                    ActiveTask.OnCompleted();
                    ActiveTask = null;
                    break;
                case TaskState.Invalid:
                    ActiveTask.OnFailed();
                    ActiveTask = null;
                    break;
            }
        }

        if (ActiveTask is null && _queue.Count > 0)
            ActiveTask = _queue.Dequeue();
    }
}
