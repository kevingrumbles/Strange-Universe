using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Strange_Universe.Game.Entities;
using StrangeUniverse.Game.Entities;

namespace StrangeUniverse.Game.Systems;

/// <summary>Circle-circle collision detection with push-out resolution for the player.</summary>
public class CollisionSystem
{
    private const float Restitution = 0.35f;

    public void Resolve(Player player, IReadOnlyList<Planet> planets, IReadOnlyList<Asteroid> asteroids)
    {
        foreach (var planet in planets)
            ResolveStatic(player, planet.Transform.Position, planet.Radius);

        foreach (var asteroid in asteroids)
            ResolveStatic(player, asteroid.Transform.Position, asteroid.Radius);
    }

    private static void ResolveStatic(Player player, Vector2 otherPos, float otherRadius)
    {
        Vector2 diff    = player.Transform.Position - otherPos;
        float   distSq  = diff.LengthSquared();
        float   minDist = player.Radius + otherRadius;

        if (distSq >= minDist * minDist || distSq == 0f)
            return;

        float   dist    = (float)System.Math.Sqrt(distSq);
        Vector2 normal  = diff / dist;
        float   overlap = minDist - dist;

        // Push player out of overlap
        player.Transform.Position += normal * overlap;

        // Cancel velocity component moving into the object
        float dot = Vector2.Dot(player.Physics.Velocity, normal);
        if (dot < 0f)
            player.Physics.Velocity -= normal * (dot * (1f + Restitution));
    }
}
