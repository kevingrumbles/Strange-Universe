using Microsoft.Xna.Framework;
using Strange_Universe.Game.Entities;
using Strange_Universe.Game.NavSystem;
using System;
using System.Collections.Generic;

using Strange_Universe.Game.Components;
namespace Strange_Universe.Game.EventSystem
{
    public class DefendedSystemSpawn : SystemEvent
    {
        public static double EventProbability = 30; // 30% chance for this event to occur
        public DefendedSystemSpawn() 
        { 
            type = EventType.SpawnEvent;
        }

        public override void ExcuteEvent(IWorldContext system)
        {
            foreach (Planet p in system.Planets)
            {
                Nonplayer npc = new Nonplayer(npcId:$"Npc_defender_{system.NpcCount+1}", shipType: new Shuttle(), jumpSpawn: false, npcName: $"Defending Shuttle");
                npc.EnqueueNavTask(new GuardTask(npc, p.Position, p.Radius * 2));
                system.AddNpc(npc);
            }
            for (int i = 0; i < 5; i++)
            {
                Nonplayer npc = new Nonplayer(npcId: $"Npc_patroler_{system.NpcCount + 1}", shipType: new Shuttle(),jumpSpawn: true, npcName: $"Patrolling Shuttle");

                // Generate random patrol points outside the asteroid belt
                List<Vector2> patrolPoints = new List<Vector2>();
                int patrolPointCount = random.Next(3, 6);
                for (int j = 0; j < patrolPointCount; j++)
                {
                    patrolPoints.Add(system.GetRandomSafeLocationOutsideAsteroidBelt(random));
                }

                npc.EnqueueNavTask(new PatrolTask(npc, patrolPoints));
                system.AddNpc(npc);
            }
        }
    }
}
