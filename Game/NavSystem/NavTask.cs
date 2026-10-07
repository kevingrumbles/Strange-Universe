using Strange_Universe.Game.Components;
using Strange_Universe.Game.Entities;

namespace Strange_Universe.Game.NavSystem
{
    public abstract class NavTask
    {
        public TaskState CurrentState { get; set; }
        public Ship Owner { get; private set; }

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
        public abstract void Update(float deltaTime);

        /// <summary>Called once by the navigator when the task reaches <see cref="TaskState.Complete"/>, before it is cleared.</summary>
        public virtual void OnCompleted() { }
    }
    
    public enum TaskState
    {
        MoveToMandeville,
        Decelerate,
        AlignForJump,
        Jump,
        SystemTranslation,
        ArriveInSystem,
        Complete,
        Invalid,
        Spawning,
        Patrolling,
        ApproachPosition,
        HoldPosition,
        Guarding,
        Attacking,
        Alerting,
    }
}
