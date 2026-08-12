using Microsoft.Xna.Framework;
using Strange_Universe.Game.Entities;
using Strange_Universe.Game.NavSystem;
using StrangeUniverse.Game.Entities;
using System;
using System.Collections.Generic;

namespace Strange_Universe.Game.EventSystem
{
    public class DefendedSystemSpawn : SystemEvent
    {
        public static double EventProbability = 30; // 30% chance for this event to occur
        public DefendedSystemSpawn() 
        { 
            type = EventType.SpawnEvent;
        }

        public override void ExcuteEvent(StarSystem system)
        {
            foreach (Planet p in system.Planets)
            {
                Nonplayer npc = new Nonplayer(npcId:$"Npc_defender_{system.Npcs.Count+1}");
                npc.EnqueueNavTask(new GuardTask(npc, p.Position, p.Radius * 2));
                system.Npcs.Add(npc);
            }
            for (int i = 0; i < 5; i++)
            {
                Nonplayer npc = new Nonplayer(npcId: $"Npc_patroler_{system.Npcs.Count + 1}",jumpSpawn: true);

                // Generate random patrol points outside the asteroid belt
                List<Vector2> patrolPoints = new List<Vector2>();
                for (int j = 0; j < random.Next(3, 6); j++)
                {
                    patrolPoints.Add(system.GetRandomSafeLocationOutsideAsteroidBelt());
                }

                npc.EnqueueNavTask(new PatrolTask(npc, patrolPoints));
                system.Npcs.Add(npc);
            }
        }
    }
}
