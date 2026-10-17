using Strange_Universe.Game.Entities;
using Strange_Universe.Game.NavSystem;
using Strange_Universe.Game.Components.ShipComponents;
using Strange_Universe.Game.Components;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Strange_Universe.Game.Entities.ShipParts;

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
            List<Vector2> patrolPoints = new List<Vector2>();
            foreach (Planet planet in system.Planets)
            {
                patrolPoints.Add(planet.Position);
            }
            Nonplayer npc = new Nonplayer(npcId: $"Npc_test_{system.NpcCount + 1}", shipType: new Shuttle(), jumpSpawn: true, npcName: $"Testing Shuttle");
            npc.EnqueueNavTask(new PatrolTask(npc, patrolPoints));
            system.AddNpc(npc);

        }
    }
}
