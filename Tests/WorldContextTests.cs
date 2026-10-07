using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Strange_Universe.Game.Components;
using Strange_Universe.Game.Entities;
using Strange_Universe.Game.EventSystem;
using Xunit;

namespace Strange_Universe.Tests;

/// <summary>Round 2, Phase 7.3: system events run against <see cref="IWorldContext"/>, not a concrete system.</summary>
public class WorldContextTests
{
    private sealed class FakeWorld : IWorldContext
    {
        public string SystemId { get; init; } = "fake_system";
        public List<Planet> PlanetList { get; } = new();
        public List<Nonplayer> Added { get; } = new();
        public List<string> Messages { get; } = new();

        public IReadOnlyList<Planet> Planets => PlanetList;
        public int NpcCount => Added.Count;
        public void AddNpc(Nonplayer npc) => Added.Add(npc);
        public Vector2 GetRandomSafeLocationOutsideAsteroidBelt() => new(100, 100);
        public void PostMessage(string message, int durationSeconds = 3) => Messages.Add(message);
    }

    private static Planet NewPlanet(int n) => new($"fake_Planet_{n}", 90f, 120f, 1000f, 2000f, n, 3);

    [Fact]
    public void MerchantEvent_AddsMerchantAndPostsMessage()
    {
        var world = new FakeWorld();
        world.PlanetList.Add(NewPlanet(1));

        new MerchantMissionEvent().ExcuteEvent(world);

        Assert.Single(world.Added);
        Assert.Single(world.Messages);
    }

    [Fact]
    public void MerchantEvent_WithoutPlanets_DoesNothing()
    {
        var world = new FakeWorld();
        new MerchantMissionEvent().ExcuteEvent(world);
        Assert.Empty(world.Added);
        Assert.Empty(world.Messages);
    }

    [Fact]
    public void DefendedSystem_SpawnsOneGuardPerPlanetPlusPatrols()
    {
        var world = new FakeWorld();
        world.PlanetList.Add(NewPlanet(1));
        world.PlanetList.Add(NewPlanet(2));

        new DefendedSystemSpawn().ExcuteEvent(world);

        Assert.Equal(2 + 5, world.Added.Count);
    }

    [Fact]
    public void EventController_IsDeterministicAgainstContext()
    {
        (int ships, string[] messages) Run()
        {
            var world = new FakeWorld();
            world.PlanetList.Add(NewPlanet(1));
            new EventController(world).Update(0.016f); // first update triggers the spawn event
            return (world.Added.Count, world.Messages.ToArray());
        }

        var a = Run();
        var b = Run();

        Assert.Equal(a.ships, b.ships);
        Assert.Equal(a.messages, b.messages);
    }
}
