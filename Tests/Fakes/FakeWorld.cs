using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Strange_Universe.Game.Components;
using Strange_Universe.Game.Entities;

namespace Strange_Universe.Tests.Fakes;

/// <summary>An <see cref="IWorldContext"/> that records what tasks and events ask of it.</summary>
public sealed class FakeWorld : IWorldContext
{
    public string SystemId { get; set; } = "fake_system";
    public float SystemRadius { get; set; } = 10000f;
    public float MandevilleRadius { get; set; } = 7500f;
    public List<Planet> PlanetList { get; } = new();
    public List<Star> StarList { get; } = new();
    public List<Nonplayer> Added { get; } = new();
    public List<string> Messages { get; } = new();
    public HashSet<string> KnownSystems { get; } = new();
    public List<string> RemovedFromRoute { get; } = new();
    public List<string> Entered { get; } = new();
    public Vector2 EdgeEntry { get; set; } = new(9000, 0);
    public Vector2 SafeEntry { get; set; } = new(123, 456);

    public IReadOnlyList<Planet> Planets => PlanetList;
    public IReadOnlyList<Star> Stars => StarList;
    public int NpcCount => Added.Count;
    public void AddNpc(Nonplayer npc) => Added.Add(npc);
    public Vector2 GetRandomSafeLocationOutsideAsteroidBelt() => new(100, 100);
    public void PostMessage(string message, int durationSeconds = 3) => Messages.Add(message);
    public Vector2 GetSystemEdgeEntryPosition(Vector2? fromGalaxyPosition = null) => EdgeEntry;
    public Transform GetSafeEntryTransform() => new() { Position = SafeEntry };
    public bool SystemExists(string systemId) => KnownSystems.Contains(systemId);
    public void RemoveFromJumpRoute(string systemId) => RemovedFromRoute.Add(systemId);
    public void EnterSystem(string systemId) { Entered.Add(systemId); SystemId = systemId; }
}
