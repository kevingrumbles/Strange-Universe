using Strange_Universe.Game.Entities;
using Strange_Universe.Game.NavSystem;

using Strange_Universe.Game.Components;
namespace Strange_Universe.Game.EventSystem
{
    public class NpcTestEvent : SystemEvent
    {
        /// <summary>
        /// Percent chance that a system spawn is this event. 0 by default: it is a test event and normally never happens.
        /// In test mode (<see cref="GameSettings.TestMode"/>) it always happens, whatever this is.
        /// </summary>
        public static double EventProbability = 0;
        public NpcTestEvent() 
        { 
            type = EventType.SpawnEvent;
        }

        public override void ExcuteEvent(IWorldContext system)
        {
            foreach (Planet planet in system.Planets)
            {
                Nonplayer npc = new Nonplayer(npcId: $"Npc_test_{system.NpcCount + 1}", shipType: ShipStats.Shuttle, jumpSpawn: true, npcName: $"Testing Shuttle");

                npc.EnqueueNavTask(new DockTask(npc, planet.Position, 9999));
                system.AddNpc(npc);
            }
        }
    }
}
