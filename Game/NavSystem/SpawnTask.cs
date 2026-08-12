using Microsoft.Xna.Framework;
using Strange_Universe.Game.Entities;
using StrangeUniverse;
using System;
using System.Linq;

namespace Strange_Universe.Game.NavSystem
{
    /// <summary>
    /// Handles automated jump navigation from current system to target system.
    /// Uses Ship's RotateTowards and ApplyThrust methods to control navigation through each state.
    /// </summary>
    public class SpawnTask : NavTask
    {
        public SpawnTask(Ship owner) : base(owner)
        {
            CurrentState = TaskState.Spawning;
        }

        public override void Update(float deltaTime)
        {
            if (Owner == null) return;

            switch (CurrentState)
            {
                case TaskState.Spawning:
                    Owner.Position = Owner.StarSystem.GetSafeEntryTransform().Position;
                    Owner.Velocity = Vector2.Zero; // Reset velocity after spawning
                    CurrentState = TaskState.Complete; // Transition to complete state after spawning
                    break;
                case TaskState.Complete:
                case TaskState.Invalid:
                    // Task is complete, remain in this state
                    break;
            }
        }
    }
}
