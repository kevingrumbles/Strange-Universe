using Strange_Universe.Game.Entities;
using Strange_Universe.Game.NavSystem;

namespace Strange_Universe.Game.EventSystem
{
    public class NpcTestEvent : SystemEvent
    {
        public static double EventProbability = 100; // 100% chance for this event to occur
        public NpcTestEvent() 
        { 
            type = EventType.SpawnEvent;
        }

        public override void ExcuteEvent(StarSystem system)
        {
            foreach (Planet planet in system.Planets)
            {
                Nonplayer npc = new Nonplayer(npcId: $"Npc_test_{system.Npcs.Count + 1}", shipType: "Shuttle", jumpSpawn: true, npcName: $"Testing Shuttle");

                npc.EnqueueNavTask(new DockTask(npc, planet.Position, 9999));
                system.AddNpc(npc);
            }
        }
    }
}
