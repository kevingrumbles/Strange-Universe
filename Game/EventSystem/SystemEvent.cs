using Strange_Universe.Game.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Strange_Universe.Game.EventSystem
{
    public abstract class SystemEvent
    {
        public Random random = new Random(new Guid().GetHashCode());
        public EventType type { get; set; }
        public abstract void ExcuteEvent(StarSystem system);
    }
}
