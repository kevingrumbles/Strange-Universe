using Strange_Universe.Game.NavSystem;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Strange_Universe.Game.Entities;

/// <summary>Nav-task queue: autopilot tasks such as spawn, dock, jump, patrol and guard.</summary>
public abstract partial class Ship
{
    [JsonIgnore] public bool HasActiveNavTask => ActiveNavTask is not null;
    [JsonIgnore] private NavTask ActiveNavTask { get; set; } = null;
    [JsonIgnore] private Queue<NavTask> NavTaskQueue { get; set; } = new Queue<NavTask>();

    public void EnqueueNavTask(NavTask task)
    {
        NavTaskQueue.Enqueue(task);
    }

    /// <summary>Advances the active nav task and starts the next queued one when it finishes.</summary>
    private void UpdateNavigation(float deltaTime)
    {
        if (ActiveNavTask is not null)
        {
            ActiveNavTask.Update(deltaTime);
            switch (ActiveNavTask.CurrentState)
            {
                case TaskState.Complete:
                    if (ActiveNavTask is JumpTask jumpTask && StarSystem.SystemId == jumpTask._targetSystemId)
                    {
                        Launcher.ActiveUniverse.JumpRoute.Remove(jumpTask._targetSystemId);
                        CurrentFuelLevel--;
                    }
                    ActiveNavTask = null;
                    break;
                case TaskState.Invalid:
                    ActiveNavTask = null;
                    break;
            }
        }
        if (ActiveNavTask is null && NavTaskQueue.Count > 0)
        {
            ActiveNavTask = NavTaskQueue.Dequeue();
        }
    }
}
