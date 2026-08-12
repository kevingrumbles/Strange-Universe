using Microsoft.Xna.Framework;
using Strange_Universe.Game.Entities;
using Strange_Universe.Game.NavSystem;
using StrangeUniverse.Game.Entities;
using System;
using System.Collections.Generic;

namespace Strange_Universe.Game.EventSystem
{
    public class MerchantMissionEvent : SystemEvent
    {
        public static double EventProbability = 100; // 100% chance for this event to occur
        public MerchantMissionEvent() 
        { 
            type = EventType.StandardEvent;
        }

        public override void ExcuteEvent(StarSystem system)
        {
            if (system.Planets.Count == 0)
            {
                return;
            }

            Nonplayer merchant = new Nonplayer(npcId: $"Npc_merchant_{system.Npcs.Count + 1}", jumpSpawn: true);
            merchant.EnqueueNavTask(new DockTask(merchant, system.Planets[random.Next(0, system.Planets.Count)].Position));
            merchant.EnqueueNavTask(new JumpTask(merchant));
            system.Npcs.Add(merchant);
            system.Universe.ShowTimedMessage("A merchant ship has arrived in the system. It is looking for a place to dock and trade.");
        }
    }
}
