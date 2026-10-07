using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Strange_Universe.Game.Entities;

namespace Strange_Universe.Game.Components;

/// <summary>
/// What system events need from the world they run in. Lets events be tested against a fake
/// instead of a concrete <see cref="StarSystem"/> and <see cref="Universe"/>.
/// </summary>
public interface IWorldContext
{
    string SystemId { get; }
    IReadOnlyList<Planet> Planets { get; }
    int NpcCount { get; }
    float SystemRadius { get; }
    float MandevilleRadius { get; }
    IReadOnlyList<Star> Stars { get; }

    /// <summary>Adds an NPC to the world and attaches it.</summary>
    void AddNpc(Nonplayer npc);

    /// <summary>A random position clear of the asteroid belt and bodies, for patrol routes.</summary>
    Vector2 GetRandomSafeLocationOutsideAsteroidBelt();

    /// <summary>Where a ship arriving from <paramref name="fromGalaxyPosition"/> crosses the system edge.</summary>
    Vector2 GetSystemEdgeEntryPosition(Vector2? fromGalaxyPosition = null);

    /// <summary>A clear position and heading for a ship appearing in the system.</summary>
    Transform GetSafeEntryTransform();

    /// <summary>True when a system with this id exists in the galaxy (a valid jump target).</summary>
    bool SystemExists(string systemId);

    /// <summary>Drops a destination from the player's selected jump route.</summary>
    void RemoveFromJumpRoute(string systemId);

    /// <summary>Makes the system with this id the active one and moves the player into it.</summary>
    void EnterSystem(string systemId);

    /// <summary>Shows a short on-screen message.</summary>
    void PostMessage(string message, int durationSeconds = 3);
}
