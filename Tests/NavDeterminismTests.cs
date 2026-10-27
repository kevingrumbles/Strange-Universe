using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Strange_Universe.Game.Components;
using Strange_Universe.Game.Components.ShipComponents;
using Strange_Universe.Game.Entities;
using Strange_Universe.Game.Entities.ShipParts;
using Strange_Universe.Game.EventSystem;
using Strange_Universe.Game.NavSystem;
using Strange_Universe.Tests.Fakes;
using Xunit;

namespace Strange_Universe.Tests;

/// <summary>Nav system review step 6: NPC decisions come from seeded generators, so the same situation plays out the same way.</summary>
public class NavDeterminismTests
{
    [Fact]
    public void NpcSpawn_IsReproducible_InARealSystem()
    {
        using var first = new NavSim("det-seed", "npc-7");
        using var second = new NavSim("det-seed", "npc-7");
        using var other = new NavSim("det-seed", "npc-8");

        Assert.Equal(first.SpawnPosition, second.SpawnPosition);
        Assert.NotEqual(first.SpawnPosition, other.SpawnPosition);
    }

    [Fact]
    public void NpcJumpIn_ArrivesAtTheSameSpot_EveryTime()
    {
        Vector2 Arrive(string seed)
        {
            using var sim = new NavSim(seed, "jumper");
            var jumper = new Nonplayer("arrival-npc", jumpSpawn: true);
            sim.System.AddNpc(jumper);
            jumper.Navigator.Update(0.016f); // starts the task
            jumper.Navigator.Update(0.016f); // runs SystemTranslation
            return jumper.Position;
        }

        Assert.Equal(Arrive("det-seed"), Arrive("det-seed"));
    }
}
