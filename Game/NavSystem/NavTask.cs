using Strange_Universe.Game.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
    }
}
