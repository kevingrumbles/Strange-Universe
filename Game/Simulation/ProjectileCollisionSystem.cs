using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Strange_Universe.Game.Entities;

namespace Strange_Universe.Game.Simulation;

/// <summary>
/// Detects projectile impacts against ships and asteroids and applies them to the
/// struck target.
///
/// Detection is swept (segment-vs-circle) rather than a simple point test, so fast
/// projectiles cannot tunnel through a target between frames. Each projectile
/// registers at most one hit; the nearest target along its path wins.
/// </summary>
public class ProjectileCollisionSystem
{
    /// <summary>
    /// Tests every live projectile against all ships and asteroids, applying each
    /// impact to the target it struck. Projectiles that hit are marked spent via
    /// <see cref="Projectile.MarkHit"/> so the owning system despawns them.
    /// </summary>
    /// <param name="projectiles">Active projectiles. Moved before this is called.</param>
    /// <param name="player">The player ship. May be null.</param>
    /// <param name="npcs">All NPC ships in the system.</param>
    /// <param name="asteroids">All asteroids in the system.</param>
    /// <param name="deltaTime">Frame time, used to reconstruct each projectile's swept path.</param>
    public void Resolve(
        IReadOnlyList<Projectile> projectiles,
        Player player,
        IReadOnlyList<Nonplayer> npcs,
        IReadOnlyList<Asteroid> asteroids,
        float deltaTime)
    {
        if (projectiles == null || projectiles.Count == 0)
            return;

        foreach (var projectile in projectiles)
        {
            if (projectile.HasHit)
                continue;

            // Reconstruct the segment travelled this frame so fast projectiles
            // cannot pass through a target between updates.
            Vector2 end   = projectile.Position;
            Vector2 start = end - projectile.Velocity * deltaTime;

            float bestT = float.MaxValue;
            Ship     bestShip     = null;
            Asteroid bestAsteroid = null;

            if (player != null && CanHit(projectile, player) &&
                TrySweep(start, end, projectile.Radius, player.Position, player.Radius, out float playerT) &&
                playerT < bestT)
            {
                bestT        = playerT;
                bestShip     = player;
                bestAsteroid = null;
            }

            if (npcs != null)
            {
                foreach (var npc in npcs)
                {
                    if (!CanHit(projectile, npc))
                        continue;

                    if (TrySweep(start, end, projectile.Radius, npc.Position, npc.Radius, out float npcT) &&
                        npcT < bestT)
                    {
                        bestT        = npcT;
                        bestShip     = npc;
                        bestAsteroid = null;
                    }
                }
            }

            if (asteroids != null)
            {
                foreach (var asteroid in asteroids)
                {
                    if (asteroid.IsDestroyed)
                        continue;

                    if (TrySweep(start, end, projectile.Radius, asteroid.Position, asteroid.Radius, out float astT) &&
                        astT < bestT)
                    {
                        bestT        = astT;
                        bestShip     = null;
                        bestAsteroid = asteroid;
                    }
                }
            }

            if (bestShip == null && bestAsteroid == null)
                continue;

            // The projectile carries all impact metadata (damage, weapon, owner),
            // so the target receives it directly.
            projectile.Position = Vector2.Lerp(start, end, bestT);

            if (bestShip != null)
                bestShip.ApplyDamage(projectile);
            else
                bestAsteroid.ApplyDamage(projectile);

            projectile.MarkHit();
        }
    }

    /// <summary>A projectile never strikes the ship that fired it, or an already-destroyed ship.</summary>
    private static bool CanHit(Projectile projectile, Ship ship)
        => ship != null && !ship.IsDestroyed && !ReferenceEquals(projectile.Owner, ship);

    /// <summary>
    /// Sweeps a circle of <paramref name="radius"/> from <paramref name="start"/> to
    /// <paramref name="end"/> against a static circle. Returns the normalized time
    /// of first contact in <paramref name="t"/> (0 = start, 1 = end).
    /// </summary>
    private static bool TrySweep(
        Vector2 start,
        Vector2 end,
        float radius,
        Vector2 center,
        float targetRadius,
        out float t)
    {
        t = 0f;

        Vector2 d = end - start;
        Vector2 f = start - center;
        float   r = radius + targetRadius;

        // Already overlapping at the start of the sweep.
        if (f.LengthSquared() <= r * r)
            return true;

        float a = Vector2.Dot(d, d);
        if (a <= float.Epsilon)
            return false;

        float b = 2f * Vector2.Dot(f, d);
        float c = Vector2.Dot(f, f) - r * r;

        float discriminant = b * b - 4f * a * c;
        if (discriminant < 0f)
            return false;

        discriminant = MathF.Sqrt(discriminant);

        // Nearest intersection along the sweep direction.
        float t0 = (-b - discriminant) / (2f * a);
        if (t0 >= 0f && t0 <= 1f)
        {
            t = t0;
            return true;
        }

        float t1 = (-b + discriminant) / (2f * a);
        if (t1 >= 0f && t1 <= 1f)
        {
            t = t1;
            return true;
        }

        return false;
    }
}
