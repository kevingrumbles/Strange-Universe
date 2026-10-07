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

    /// <summary>Adds an NPC to the world and attaches it.</summary>
    void AddNpc(Nonplayer npc);

    /// <summary>A random position clear of the asteroid belt and bodies, for patrol routes.</summary>
    Vector2 GetRandomSafeLocationOutsideAsteroidBelt();

    /// <summary>Shows a short on-screen message.</summary>
    void PostMessage(string message, int durationSeconds = 3);
}
