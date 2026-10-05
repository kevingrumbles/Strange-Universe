using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Strange_Universe.Game.Entities.ShipParts;

/// <summary>
/// Spatial awareness and targeting for a ship. Avoids LINQ and per-call
/// allocations on the per-frame paths (target validation, closest queries).
/// </summary>
public class ShipSensors
{
    private readonly Ship _owner;

    public ShipSensors(Ship owner)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    public Ship Target { get; set; }

    private StarSystem System => _owner.StarSystem;

    public float DistanceTo(Vector2 position) => Vector2.Distance(_owner.Position, position);

    #region Targeting
    /// <summary>Sets the current target. Does not allow targeting self.</summary>
    public void SetTarget(Ship target)
    {
        if (target == _owner)
            return;
        Target = target;
    }

    public void ClearTarget() => Target = null;

    /// <summary>True if the target is not null, not self, and still present in the system.</summary>
    public bool IsTargetValid(Ship target)
    {
        if (target == null || target == _owner || System == null)
            return false;

        if (target == System.ActivePlayer)
            return true;

        var npcs = System.Npcs;
        for (int i = 0; i < npcs.Count; i++)
        {
            if (npcs[i] == target)
                return true;
        }
        return false;
    }

    /// <summary>The nearest ship in the current system, excluding self.</summary>
    public Ship GetNearestTarget()
    {
        if (System == null)
            return null;

        Ship nearest = null;
        float nearestDistance = float.MaxValue;

        var npcs = System.Npcs;
        for (int i = 0; i < npcs.Count; i++)
            Consider(npcs[i], ref nearest, ref nearestDistance);

        Consider(System.ActivePlayer, ref nearest, ref nearestDistance);
        return nearest;
    }

    private void Consider(Ship ship, ref Ship nearest, ref float nearestDistance)
    {
        if (ship == null || ship == _owner)
            return;

        float distance = DistanceTo(ship.Position);
        if (distance < nearestDistance)
        {
            nearestDistance = distance;
            nearest = ship;
        }
    }
    #endregion

    #region Nearby / closest
    /// <summary>All ships within range, excluding self.</summary>
    public IReadOnlyList<Ship> GetNearbyShips(float range = float.MaxValue)
    {
        if (System == null)
            return Array.Empty<Ship>();

        var nearby = new List<Ship>();
        foreach (var npc in System.Npcs)
        {
            if (npc != _owner && DistanceTo(npc.Position) <= range)
                nearby.Add(npc);
        }

        var player = System.ActivePlayer;
        if (player != null && player != _owner && DistanceTo(player.Position) <= range)
            nearby.Add(player);

        return nearby;
    }

    public IReadOnlyList<Planet> GetNearbyPlanets(float range = float.MaxValue)
        => Within(System?.Planets, p => p.Position, range);

    public IReadOnlyList<Star> GetNearbyStars(float range = float.MaxValue)
        => Within(System?.Stars, s => s.Position, range);

    public IReadOnlyList<Asteroid> GetNearbyAsteroids(float range = float.MaxValue)
        => Within(System?.Asteroids, a => a.Position, range);

    public Planet GetClosestPlanet() => Closest(System?.Planets, p => p.Position);

    public Star GetClosestStar() => Closest(System?.Stars, s => s.Position);

    public Ship GetClosestShip() => GetNearestTarget();

    private IReadOnlyList<T> Within<T>(List<T> items, Func<T, Vector2> position, float range)
    {
        if (items == null)
            return Array.Empty<T>();

        var result = new List<T>();
        for (int i = 0; i < items.Count; i++)
        {
            if (DistanceTo(position(items[i])) <= range)
                result.Add(items[i]);
        }
        return result;
    }

    /// <summary>Single-pass minimum; first item wins ties, matching OrderBy().FirstOrDefault().</summary>
    private T Closest<T>(List<T> items, Func<T, Vector2> position) where T : class
    {
        if (items == null)
            return null;

        T best = null;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < items.Count; i++)
        {
            float d = DistanceTo(position(items[i]));
            if (best == null || d < bestDistance)
            {
                best = items[i];
                bestDistance = d;
            }
        }
        return best;
    }
    #endregion

    #region Collision prediction
    /// <summary>
    /// True if the current velocity is heading toward <paramref name="position"/> and
    /// will be within <paramref name="dangerRadius"/> after <paramref name="lookAheadTime"/>.
    /// </summary>
    public bool IsApproachingCollision(Vector2 position, float dangerRadius, float lookAheadTime = 5f)
    {
        float distance = DistanceTo(position);

        if (distance > dangerRadius * 2f)
            return false;

        Vector2 toPosition = position - _owner.Position;
        if (toPosition.LengthSquared() < 0.1f)
            return true;

        Vector2 direction = Vector2.Normalize(toPosition);
        float velocityTowardPosition = Vector2.Dot(_owner.Velocity, direction);

        if (velocityTowardPosition <= 0)
            return false;

        float futureDistance = distance - velocityTowardPosition * lookAheadTime;
        return futureDistance <= dangerRadius;
    }

    public bool IsApproachingPlanetCollision(float lookAheadTime = 5f)
    {
        var planets = System?.Planets;
        if (planets == null) return false;

        for (int i = 0; i < planets.Count; i++)
        {
            if (IsApproachingCollision(planets[i].Position, planets[i].Radius * 1.5f, lookAheadTime))
                return true;
        }
        return false;
    }

    public bool IsApproachingStarCollision(float lookAheadTime = 5f)
    {
        var stars = System?.Stars;
        if (stars == null) return false;

        for (int i = 0; i < stars.Count; i++)
        {
            if (IsApproachingCollision(stars[i].Position, stars[i].Radius * 1.5f, lookAheadTime))
                return true;
        }
        return false;
    }
    #endregion
}
