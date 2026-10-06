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

    public void Enqueue(NavTask task) => _queue.Enqueue(task);

    /// <summary>Advances the active nav task and starts the next queued one when it finishes.</summary>
    public void Update(float deltaTime)
    {
        if (ActiveTask is not null)
        {
            ActiveTask.Update(deltaTime);
            switch (ActiveTask.CurrentState)
            {
                case TaskState.Complete:
                    var system = _owner.StarSystem;
                    if (ActiveTask is JumpTask jumpTask && system != null && system.SystemId == jumpTask._targetSystemId)
                    {
                        system.Universe?.JumpRoute.Remove(jumpTask._targetSystemId);
                        _owner.CurrentFuelLevel--;
                    }
                    ActiveTask = null;
                    break;
                case TaskState.Invalid:
                    ActiveTask = null;
                    break;
            }
        }

        if (ActiveTask is null && _queue.Count > 0)
            ActiveTask = _queue.Dequeue();
    }
}
