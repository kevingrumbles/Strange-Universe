using Microsoft.Xna.Framework;
using Strange_Universe.Game.Entities;
using Strange_Universe.Game.NavSystem;
using Strange_Universe;
using System;
using System.Collections.Generic;

using Strange_Universe.Game.Components;
namespace Strange_Universe.Game.EventSystem
{
    public class ScoutedSystemSpawn : SystemEvent
    {
        public static double EventProbability = 100; // 100% chance for this event to occur
        public ScoutedSystemSpawn() 
        { 
            type = EventType.SpawnEvent;
        }

        public override void ExcuteEvent(IWorldContext system)
        {
            for (int i = 0; i < 1; i++)
            {
                Nonplayer npc = new Nonplayer(npcId: $"Npc_patroler_{system.NpcCount + 1}", shipType: "Shuttle", jumpSpawn: true, npcName: $"Patrolling Shuttle");

                // Generate random patrol points outside the asteroid belt
                List<Vector2> patrolPoints = new List<Vector2>();
                for (int j = 0; j < random.Next(3, 6); j++)
                {
                    patrolPoints.Add(system.GetRandomSafeLocationOutsideAsteroidBelt());
                }

                if (patrolPoints.Count == 0)
                {
                    system.PostMessage($"No valid patrol points for {npc.Id}");
                }

                npc.EnqueueNavTask(new PatrolTask(npc, patrolPoints));
                system.AddNpc(npc);
            }
        }
    }
}
