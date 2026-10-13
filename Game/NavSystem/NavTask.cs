using System;
using Strange_Universe.Game.Components;
using Strange_Universe.Game.Entities;

namespace Strange_Universe.Game.NavSystem
{
    /// <summary>Whether a nav task is still running, or how it ended.</summary>
    public enum NavTaskStatus
    {
        /// <summary>The task is flying. Its own phase (if it has several) says what it is doing.</summary>
        Running,

        /// <summary>The task did what it was for. The navigator calls <see cref="NavTask.OnCompleted"/>.</summary>
        Complete,

        /// <summary>The task can not be carried out. The navigator calls <see cref="NavTask.OnFailed"/>.</summary>
        Invalid,
    }

    /// <summary>
    /// An autopilot job for a ship. The navigator only needs to know whether it is still
    /// <see cref="NavTaskStatus.Running"/>; tasks with several stages keep their own phase enum
    /// (for example <see cref="JumpTask.Phase"/>), which only that task can change.
    /// </summary>
    public abstract class NavTask
    {
        public Ship Owner { get; private set; }

        /// <summary>Running until the task finishes itself with <see cref="Complete"/> or <see cref="Fail"/>.</summary>
        public NavTaskStatus Status { get; private set; } = NavTaskStatus.Running;

        public bool IsFinished => Status != NavTaskStatus.Running;

        private IWorldContext _world;

        /// <summary>The world the owner is in. Defaults to the owner's system; assign a fake to test a task in isolation.</summary>
        public IWorldContext World
        {
            get => _world ?? Owner?.StarSystem;
            set => _world = value;
        }

        public NavTask(Ship owner)
        {
            Owner = owner;
        }

        /// <summary>
        /// A random generator for NPC decisions, seeded from the ship, the system and <paramref name="purpose"/>, so the same
        /// situation always plays out the same way. Returns null for the player, whose arrival and spawn spots stay
        /// varied: callers pass the result to the world, which then uses its own generator.
        /// </summary>
        protected Random RandomFor(string purpose)
        {
            if (Owner is null or Player)
                return null;

            return new Random(ProceduralHelpers.SeedHash($"{Owner.Id ?? Owner.Name}:{World?.SystemId}:{purpose}"));
        }

        /// <summary>Advances the task by one frame. The navigator only calls this while the task is running.</summary>
        public abstract void Update(float deltaTime);

        /// <summary>Marks the task as done. Has no effect once the task has finished.</summary>
        protected void Complete()
        {
            if (Status == NavTaskStatus.Running) Status = NavTaskStatus.Complete;
        }

        /// <summary>Marks the task as impossible. Has no effect once the task has finished.</summary>
        protected void Fail()
        {
            if (Status == NavTaskStatus.Running) Status = NavTaskStatus.Invalid;
        }

        /// <summary>Called once by the navigator when the task has completed, before it is cleared.</summary>
        public virtual void OnCompleted() { }

        /// <summary>Called once by the navigator when the task has failed, before it is cleared. Not called when a task is cancelled.</summary>
        public virtual void OnFailed() { }
    }
}
