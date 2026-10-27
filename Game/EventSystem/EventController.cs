using System;
using System.Linq;
using Strange_Universe.Game.Components;
using Strange_Universe.Game.Components.ShipComponents;
using Strange_Universe.Game.Entities.ShipParts;
using Strange_Universe.Game.NavSystem;

namespace Strange_Universe.Game.EventSystem;

/// <summary>
/// Makes things happen in a star system as time passes: one spawn when the system is entered (an NPC test, a defended system, or a scouted one) and a
/// standard event now and then (a merchant). Every roll comes from a generator seeded by the system, so a system's events always play out the same way,
/// and each event gets its own generator drawn from that stream (<see cref="NextEventRandom"/>), which it passes on to the world for anything random.
/// </summary>
public class EventController
{
    // The chance, in percent, of each event. The spawn events are tried in order and each takes its share of the roll (0-99): the first one whose
    // number is above the roll happens, so a later chance is a cumulative one (scouted: 100 means "everything the earlier ones did not take").
    private const double NpcTestProbability = 0;      // normally never; always in test mode (GameSettings.TestMode)
    private const double DefendedProbability = 30;
    private const double ScoutedProbability = 100;
    private const double MerchantProbability = 100;

    private readonly IWorldContext _owner;
    private float? _standardEventThreshold = null;
    private float _timeSinceLastEvent = 0f;
    private readonly Random _random;
    private const int MinEventThreshold = 60;
    private const int MaxEventThreshold = 61;

    public EventController(IWorldContext owner)
    {
        _random = new Random(ProceduralHelpers.SeedHash($"{owner.SystemId}:Events"));
        _owner = owner;
    }

    public void Update(float deltaTime)
    {
        if (_owner == null) return;

        _timeSinceLastEvent += deltaTime;
        if (_standardEventThreshold is null)
        {
            SpawnEvent();
            _timeSinceLastEvent = 0f;
            _standardEventThreshold = _random.Next(MinEventThreshold, MaxEventThreshold);
        }
        else if (_timeSinceLastEvent >= _standardEventThreshold)
        {
            StandardEvent();

            // Set a new random threshold for the next event
            _timeSinceLastEvent = 0f;
            _standardEventThreshold = _random.Next(MinEventThreshold, MaxEventThreshold);
        }
    }

    /// <summary>A generator for the next event, drawn from the controller's own seeded stream.</summary>
    private Random NextEventRandom() => new Random(_random.Next());

    /// <summary>Share of system spawns (percent) taken by the NPC test event: always 100 in test mode, otherwise <c>NpcTestProbability</c> (0).</summary>
    private double CurrentNpcTestProbability => _owner.TestMode ? 100 : NpcTestProbability;

    public void SpawnEvent()
    {
        if (_owner == null) return;

        // The NPC test event comes first, so it takes its share of the rolls before the others. In test mode that
        // share is everything: every system spawn is the test event.
        int roll = (int)(_random.NextDouble() * 100);
        if (roll < CurrentNpcTestProbability)
        {
            SpawnNpcTest(_owner);
        }
        else if (roll < DefendedProbability)
        {
            _owner.PostMessage($"A Defended System has been discovered! {roll}/{DefendedProbability}");
            SpawnDefended(_owner, NextEventRandom());
        }
        else if (roll < ScoutedProbability)
        {
            _owner.PostMessage($"A Scouted System has been discovered! {roll}/{ScoutedProbability}");
            SpawnScouted(_owner, NextEventRandom());
        }
    }

    public void StandardEvent()
    {
        if (_owner == null) return;

        if ((int)(_random.NextDouble() * 100) < MerchantProbability)
            MerchantMission(_owner, NextEventRandom());
    }

    // -- The events -----------------------------------------------------------------

    /// <summary>A shuttle that patrols between all the planets, to try things on.</summary>
    public static void SpawnNpcTest(IWorldContext world)
    {
        var patrolPoints = world.Planets.Select(planet => planet.Position).ToList();
        var npc = new Nonplayer(npcId: $"Npc_test_{world.NpcCount + 1}", shipType: new Shuttle(), jumpSpawn: true, npcName: "Testing Shuttle");
        npc.EnqueueNavTask(new PatrolTask(npc, patrolPoints));
        world.AddNpc(npc);
    }

    /// <summary>A guard orbiting every planet, and five patrols.</summary>
    public static void SpawnDefended(IWorldContext world, Random random)
    {
        foreach (var planet in world.Planets)
        {
            var guard = new Nonplayer(npcId: $"Npc_defender_{world.NpcCount + 1}", shipType: new Shuttle(), jumpSpawn: false, npcName: "Defending Shuttle");
            guard.EnqueueNavTask(new GuardTask(guard, planet.Position, planet.Radius * 2));
            world.AddNpc(guard);
        }
        for (int i = 0; i < 5; i++)
            SpawnPatrol(world, random);
    }

    /// <summary>One patrol.</summary>
    public static void SpawnScouted(IWorldContext world, Random random) => SpawnPatrol(world, random);

    /// <summary>A merchant that arrives, docks at a planet and leaves again. Does nothing in a system with no planets.</summary>
    public static void MerchantMission(IWorldContext world, Random random)
    {
        if (world.Planets.Count == 0)
            return;

        var merchant = new Nonplayer(npcId: $"Npc_merchant_{world.NpcCount + 1}", shipType: new Shuttle(), jumpSpawn: true, npcName: "Merchant Shuttle");
        merchant.EnqueueNavTask(new DockTask(merchant, world.Planets[random.Next(0, world.Planets.Count)].Position));
        merchant.EnqueueNavTask(new JumpTask(merchant));
        world.AddNpc(merchant);
        world.PostMessage("A merchant ship has arrived in the system. It is looking for a place to dock and trade.");
    }

    /// <summary>A shuttle arriving by jump that patrols three to five random points outside the asteroid belt. The count is rolled once, before the points.</summary>
    private static void SpawnPatrol(IWorldContext world, Random random)
    {
        var npc = new Nonplayer(npcId: $"Npc_patroler_{world.NpcCount + 1}", shipType: new Shuttle(), jumpSpawn: true, npcName: "Patrolling Shuttle");
        int pointCount = random.Next(3, 6);
        var patrolPoints = Enumerable.Range(0, pointCount).Select(_ => world.GetRandomSafeLocationOutsideAsteroidBelt(random)).ToList();
        npc.EnqueueNavTask(new PatrolTask(npc, patrolPoints));
        world.AddNpc(npc);
    }
}
