using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Strange_Universe.Game.Entities;
using StrangeUniverse.Game.Entities;

namespace StrangeUniverse.Game.Systems;

/// <summary>Circle-circle collision detection with push-out resolution for ships.</summary>
public class CollisionSystem
{
    private const float Restitution = 0.35f;

    /// <summary>
    /// Resolves collisions for the player against static objects (planets, asteroids).
    /// </summary>
    public void Resolve(Player player, IReadOnlyList<Planet> planets, IReadOnlyList<Asteroid> asteroids)
    {
        if (Launcher.PlanetColision)
        {
            foreach (var planet in planets)
                ResolveShipStatic(player, planet.Position, planet.Radius);
        }

        foreach (var asteroid in asteroids)
            ResolveShipStatic(player, asteroid.Position, asteroid.Radius);
    }

    /// <summary>
    /// Resolves collisions for an NPC against static objects (planets, asteroids).
    /// </summary>
    public void ResolveNPC(Nonplayer npc, IReadOnlyList<Planet> planets, IReadOnlyList<Asteroid> asteroids)
    {
        if (Launcher.PlanetColision)
        {
            foreach (var planet in planets)
                ResolveShipStatic(npc, planet.Position, planet.Radius);
        }

        foreach (var asteroid in asteroids)
            ResolveShipStatic(npc, asteroid.Position, asteroid.Radius);
    }

    /// <summary>
    /// Resolves ship-to-ship collisions between all ships (player and NPCs).
    /// </summary>
    public void ResolveShipToShip(Player player, IReadOnlyList<Nonplayer> npcs)
    {
        // Player vs NPCs
        foreach (var npc in npcs)
        {
            ResolveShipToShip(player, npc);
        }

        // NPC vs NPC
        for (int i = 0; i < npcs.Count; i++)
        {
            for (int j = i + 1; j < npcs.Count; j++)
            {
                ResolveShipToShip(npcs[i], npcs[j]);
            }
        }
    }

    /// <summary>
    /// Resolves collision between a ship and a static object (planet or asteroid).
    /// </summary>
    private static void ResolveShipStatic(Ship ship, Vector2 otherPos, float otherRadius)
    {
        Vector2 diff    = ship.Position - otherPos;
        float   distSq  = diff.LengthSquared();
        float   minDist = ship.Radius + otherRadius;

        if (distSq >= minDist * minDist || distSq == 0f)
            return;

        float   dist    = (float)System.Math.Sqrt(distSq);
        Vector2 normal  = diff / dist;
        float   overlap = minDist - dist;

        // Push ship out of overlap
        ship.Position += normal * overlap;

        // Cancel velocity component moving into the object
        float dot = Vector2.Dot(ship.Velocity, normal);
        if (dot < 0f)
            ship.Velocity -= normal * (dot * (1f + Restitution));
    }

    /// <summary>
    /// Resolves collision between two ships.
    /// Both ships are pushed apart and velocities are adjusted.
    /// </summary>
    private static void ResolveShipToShip(Ship shipA, Ship shipB)
    {
        Vector2 diff    = shipA.Position - shipB.Position;
        float   distSq  = diff.LengthSquared();
        float   minDist = shipA.Radius + shipB.Radius;

        if (distSq >= minDist * minDist || distSq == 0f)
            return;

        float   dist    = (float)System.Math.Sqrt(distSq);
        Vector2 normal  = diff / dist;
        float   overlap = minDist - dist;

        // Push both ships apart equally (each gets half the overlap correction)
        Vector2 correction = normal * (overlap * 0.5f);
        shipA.Position += correction;
        shipB.Position -= correction;

        // Calculate relative velocity
        Vector2 relativeVelocity = shipA.Velocity - shipB.Velocity;
        float velocityAlongNormal = Vector2.Dot(relativeVelocity, normal);

        // Only resolve if ships are moving toward each other
        if (velocityAlongNormal > 0f)
            return;

        // Apply impulse to both ships (simplified elastic collision)
        float impulse = -(1f + Restitution) * velocityAlongNormal / 2f;
        Vector2 impulseVector = normal * impulse;

        shipA.Velocity += impulseVector;
        shipB.Velocity -= impulseVector;
    }
}
