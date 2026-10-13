using Strange_Universe.Game.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Strange_Universe.Game.Components;
namespace Strange_Universe.Game.EventSystem
{
    public abstract class SystemEvent
    {
        /// <summary>
        /// Random source for this event. <see cref="EventController"/> assigns a generator seeded from the system and the
        /// event's place in the sequence, so a system's events always play out the same way. (The default is only for
        /// events run outside a controller, and is deliberately fixed rather than time based.)
        /// </summary>
        public Random random = new Random(0);
        public EventType type { get; set; }
        public abstract void ExcuteEvent(IWorldContext system);
    }
}
