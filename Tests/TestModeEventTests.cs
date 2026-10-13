using System.Linq;
using Strange_Universe.Game.Components;
using Strange_Universe.Game.Entities;
using Strange_Universe.Game.EventSystem;
using Strange_Universe.Tests.Fakes;
using Xunit;

namespace Strange_Universe.Tests;

/// <summary>Test mode makes the system spawn event always the NPC test event; otherwise events use their own probabilities.</summary>
public class TestModeEventTests
{
    private static FakeWorld WorldFor(string systemId, bool testMode)
    {
        var world = new FakeWorld { SystemId = systemId, TestMode = testMode };
        for (int i = 1; i <= 3; i++) world.PlanetList.Add(new Planet($"{systemId}_p{i}", 90f, 120f, 1000f, 2000f, i, 3));
        return world;
    }

    /// <summary>Runs the first controller update, which is the system spawn event.</summary>
    private static FakeWorld Spawn(string systemId, bool testMode, double? probabilityOverride = null)
    {
        var world = WorldFor(systemId, testMode);
        new EventController(world) { NpcTestProbabilityOverride = probabilityOverride }.Update(0.016f);
        return world;
    }

    private static bool HasTestNpcs(FakeWorld world) => world.Added.Any(n => n.Id.StartsWith("Npc_test_"));

    // -- test mode on -------------------------------------------------------

    [Fact]
    public void TestMode_EverySystemSpawn_IsTheNpcTestEvent()
    {
        foreach (int i in Enumerable.Range(0, 100))
        {
            var world = Spawn($"seed_system_{i}", testMode: true);

            Assert.True(HasTestNpcs(world), $"system {i} had no test NPCs");
            Assert.Equal(world.PlanetList.Count, world.Added.Count);          // one per planet
            Assert.All(world.Added, n => Assert.StartsWith("Npc_test_", n.Id)); // and nothing else spawned
            Assert.Empty(world.Messages);                                       // no "system discovered" message
        }
    }

    [Fact]
    public void TestMode_PreemptsTheNormalSpawnEvents()
    {
        // Without test mode, these same systems do spawn the normal events (patrols or guards).
        var normal = Enumerable.Range(0, 100).Select(i => Spawn($"seed_system_{i}", testMode: false)).ToList();
        Assert.Contains(normal, w => w.Added.Count > 0 && !HasTestNpcs(w));

        var test = Enumerable.Range(0, 100).Select(i => Spawn($"seed_system_{i}", testMode: true)).ToList();
        Assert.All(test, w => Assert.DoesNotContain(w.Added, n => n.Id.Contains("patroler") || n.Id.Contains("defender")));
    }

    // -- test mode off ------------------------------------------------------

    [Fact]
    public void NormalMode_NeverSpawnsTestNpcs_WithTheDefaultProbability()
    {
        Assert.Equal(0, NpcTestEvent.EventProbability);

        foreach (int i in Enumerable.Range(0, 300))
            Assert.False(HasTestNpcs(Spawn($"seed_system_{i}", testMode: false)));
    }

    [Fact]
    public void NormalMode_IsExactlyAsItWasBeforeTheTestEventExisted()
    {
        // With the test event at 0%, a roll never lands on it and no extra random draw is taken,
        // so every system plays out the same as with the event switched off entirely.
        foreach (int i in Enumerable.Range(0, 60))
        {
            var normal = Spawn($"seed_system_{i}", testMode: false);
            var disabled = Spawn($"seed_system_{i}", testMode: false, probabilityOverride: -1);

            Assert.Equal(disabled.Added.Select(n => n.Id), normal.Added.Select(n => n.Id));
            Assert.Equal(disabled.Messages, normal.Messages);
        }
    }

    [Fact]
    public void NormalMode_UsesTheEventsProbability_LikeAnyOtherEvent()
    {
        int hits = Enumerable.Range(0, 400).Count(i => HasTestNpcs(Spawn($"seed_system_{i}", testMode: false, probabilityOverride: 50)));
        Assert.InRange(hits, 140, 260); // about half

        Assert.All(Enumerable.Range(0, 50), i => Assert.True(HasTestNpcs(Spawn($"seed_system_{i}", testMode: false, probabilityOverride: 100))));
        Assert.All(Enumerable.Range(0, 50), i => Assert.False(HasTestNpcs(Spawn($"seed_system_{i}", testMode: false, probabilityOverride: 0))));
    }

    [Fact]
    public void AnExplicitProbability_WinsOverTestMode()
    {
        Assert.False(HasTestNpcs(Spawn("seed_system_1", testMode: true, probabilityOverride: 0)));
    }

    // -- wiring -------------------------------------------------------------

    [Fact]
    public void LauncherTestMode_DefaultsToOff()
    {
        Assert.False(Launcher.TestMode);
        Assert.False(new GameSettings().TestMode);
    }

    [Fact]
    public void TheWorld_ReportsTheSettingsTestMode()
    {
        var u = new Universe("TM", "tm-seed") { NebulaFactory = Nebula.CreateWithoutPixels };
        u.Generate(new RecordingAssetRequests());
        IWorldContext world = u.ActiveStarSystem;
        Assert.False(world.TestMode);

        u.Settings.TestMode = true;
        Assert.True(world.TestMode);
        u.Dispose();
    }

    [Fact]
    public void InARealSystem_TestMode_SpawnsTestNpcsOnTheFirstUpdate()
    {
        var u = new Universe("TM", "tm-real") { NebulaFactory = Nebula.CreateWithoutPixels };
        u.Settings.TestMode = true;
        u.Generate(new RecordingAssetRequests());
        var system = u.ActiveStarSystem;
        u.Player.Navigator.Clear();

        system.Update(1f / 60f, new InputState());

        Assert.Equal(system.Planets.Count, system.Npcs.Count(n => n.Id.StartsWith("Npc_test_")));
        Assert.All(system.Npcs, n => Assert.NotEmpty(n.Equipment)); // and they are armed, so they can be used for alert testing
        u.Dispose();
    }
}
