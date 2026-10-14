using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Strange_Universe.Game.Components;
using Strange_Universe.Game.Entities;
using Strange_Universe.Game.EventSystem;
using Strange_Universe.Game.NavSystem;
using Strange_Universe.Tests.Fakes;
using Xunit;

namespace Strange_Universe.Tests;

/// <summary>Nav system review step 6: NPC decisions come from seeded generators, so the same situation plays out the same way.</summary>
public class NavDeterminismTests
{
    private static Planet NewPlanet(int n) => new($"p_{n}", 90f, 120f, 1000f, 2000f, n, 4);

    // -- events -------------------------------------------------------------

    private static (int ships, string[] messages, int[] randomsNull) RunEvents(string systemId, float seconds)
    {
        var world = new FakeWorld { SystemId = systemId };
        for (int i = 1; i <= 4; i++) world.PlanetList.Add(NewPlanet(i));
        var controller = new EventController(world);

        for (float t = 0; t < seconds; t += 1f) controller.Update(1f);

        return (world.Added.Count, world.Messages.ToArray(), world.RandomsUsed.Select(r => r == null ? 1 : 0).ToArray());
    }

    [Fact]
    public void EventController_PlaysOutTheSameWayForTheSameSystem()
    {
        var a = RunEvents("seed_Sol", 300f);
        var b = RunEvents("seed_Sol", 300f);

        Assert.True(a.ships > 0, "the run should have produced some events");
        Assert.Equal(a.ships, b.ships);
        Assert.Equal(a.messages, b.messages);
    }

    [Fact]
    public void EventController_GivesEveryEventItsOwnSeededGenerator_ToTheWorld()
    {
        var a = RunEvents("seed_Sol", 300f);

        Assert.NotEmpty(a.randomsNull);
        Assert.All(a.randomsNull, isNull => Assert.Equal(0, isNull)); // never null: the world must not fall back to its own
    }

    [Fact]
    public void Event_PassesItsGeneratorToTheWorld_ForPatrolPoints()
    {
        var world = new FakeWorld();
        var mine = new Random(5);

        new ScoutedSystemSpawn { random = mine }.ExcuteEvent(world);

        Assert.NotEmpty(world.RandomsUsed);
        Assert.All(world.RandomsUsed, r => Assert.Same(mine, r));
    }

    [Fact]
    public void Event_PatrolPointCount_IsDecidedOnce()
    {
        // The loop bound used to be re-rolled on every iteration, so the count was erratic.
        foreach (int seed in Enumerable.Range(0, 40))
        {
            var world = new FakeWorld();
            var rng = new Random(seed);
            int expected = new Random(seed).Next(3, 6);

            new ScoutedSystemSpawn { random = rng }.ExcuteEvent(world);

            Assert.Equal(expected, world.RandomsUsed.Count);
        }
    }

    // -- tasks --------------------------------------------------------------

    [Fact]
    public void SpawnTask_Npc_GetsASeededGenerator_Player_DoesNot()
    {
        var world = new FakeWorld { SystemId = "sys" };

        new SpawnTask(new Nonplayer("npc-1")) { World = world }.Update(0.016f);
        new SpawnTask(new Player("p", ShipStats.Shuttle)) { World = world }.Update(0.016f);

        Assert.NotNull(world.RandomsUsed[0]);
        Assert.Null(world.RandomsUsed[1]); // the player keeps the world's own varied generator
    }

    [Fact]
    public void SpawnTask_SameNpcAndSystem_DrawsTheSameValues_DifferentNpcDoesNot()
    {
        FakeWorld Run(string id, string system)
        {
            var w = new FakeWorld { SystemId = system };
            new SpawnTask(new Nonplayer(id)) { World = w }.Update(0.016f);
            return w;
        }

        double First(FakeWorld w) => w.RandomsUsed[0].NextDouble();

        Assert.Equal(First(Run("a", "s1")), First(Run("a", "s1")));
        Assert.NotEqual(First(Run("a", "s1")), First(Run("b", "s1")));
        Assert.NotEqual(First(Run("a", "s1")), First(Run("a", "s2")));
    }

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
