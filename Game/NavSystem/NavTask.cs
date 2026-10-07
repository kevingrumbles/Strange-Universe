using Strange_Universe.Game.Entities;

namespace Strange_Universe.Game.NavSystem
{
    public abstract class NavTask
    {
        public TaskState CurrentState { get; set; }
        public Ship Owner { get; private set; }
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
