using Microsoft.Xna.Framework;
using Strange_Universe.Game.Components;
using System;

namespace Strange_Universe.Game.Entities;

/// <summary>
/// Spatial queries against a star system: gravity, safe positions and entry points.
/// Randomness comes from an injected <see cref="Random"/> so tests can seed it.
/// </summary>
public class SystemSpatialQueries
{
    private readonly StarSystem _system;
    private readonly Random _random;

    public SystemSpatialQueries(StarSystem system, Random random = null)
    {
        _system = system;
        _random = random ?? new Random();
    }

    /// <summary>
    /// Calculates the total gravitational force at a given location by summing
    /// all overlapping gravity wells from stars and planets in the system.
    /// </summary>
    public Vector2 CalculateGravityAtLocation(Vector2 position)
    {
        Vector2 totalForce = Vector2.Zero;
        float referenceThrust = _system.ActivePlayer?.ShipType?.ThrustForce ?? 0f;

        foreach (var star in _system.Stars)
            totalForce += star.GravityWell.CalculateForce(position, referenceThrust);

        foreach (var planet in _system.Planets)
            totalForce += planet.GravityWell.CalculateForce(position, referenceThrust);

        return totalForce;
    }

    /// <summary>
    /// Calculates an entry position at the edge of the system radius based on incoming direction
    /// from a galaxy position, with random variance on angle and distance.
    /// </summary>
    public Vector2 GetSystemEdgeEntryPosition(Vector2? fromGalaxyPosition = null)
    {
        float baseAngle;
        if (fromGalaxyPosition.HasValue)
        {
            Vector2 galaxyVector = _system.GalaxyPosition - fromGalaxyPosition.Value;
            baseAngle = (float)Math.Atan2(galaxyVector.Y, galaxyVector.X);
        }
        else
        {
            baseAngle = (float)(_random.NextDouble() * Math.PI * 2);
        }

        // Angular variance ±15 degrees (±0.26 radians)
        float angleVariance = (float)((_random.NextDouble() - 0.5) * 0.52);
        float entryAngle = baseAngle + angleVariance;
        Vector2 entryDirection = new Vector2((float)Math.Cos(entryAngle), (float)Math.Sin(entryAngle));

        // Distance variance ±5% of system radius
        float distanceVariance = (float)((_random.NextDouble() - 0.5) * 0.1);
        float entryDistance = _system.SystemRadius * (1f + distanceVariance);

        return entryDirection * entryDistance;
    }

    /// <summary>
    /// A position just inside the Mandeville radius, facing the system centre.
    /// </summary>
    public Transform GetSafeEntryTransform(float? arrivalAngle = null)
    {
        float angle = arrivalAngle ?? (float)(_random.NextDouble() * Math.PI * 2);

        Vector2 outwardDirection = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
        Vector2 entryPosition = outwardDirection * (_system.MandevilleRadius * 0.9f);

        Vector2 inwardDirection = -outwardDirection;
        float rotation = (float)Math.Atan2(inwardDirection.Y, inwardDirection.X);

        return new Transform
        {
            Position = entryPosition,
            Rotation = rotation,
            Scale = 1f
        };
    }

    /// <summary>
    /// Returns <paramref name="location"/> pushed clear of all stars and planets.
    /// If the location is already safe, returns it unchanged.
    /// </summary>
    public Vector2 GetSafeLocation(Vector2 location)
    {
        const float MinimumClearance = 1000f;
        Vector2 safeLocation = location;
        bool needsAdjustment = true;
        int maxAttempts = 10;
        int attempts = 0;

        while (needsAdjustment && attempts < maxAttempts)
        {
            needsAdjustment = false;
            attempts++;

            if (_system.Stars != null)
            {
                foreach (var star in _system.Stars)
                    needsAdjustment |= PushClear(ref safeLocation, star.Position, star.Radius + MinimumClearance);
            }

            if (_system.Planets != null)
            {
                foreach (var planet in _system.Planets)
                    needsAdjustment |= PushClear(ref safeLocation, planet.Position, planet.Radius + MinimumClearance);
            }
        }

        return safeLocation;
    }

    private bool PushClear(ref Vector2 location, Vector2 body, float requiredDistance)
    {
        if (Vector2.Distance(location, body) >= requiredDistance)
            return false;

        Vector2 away = location - body;
        if (away.LengthSquared() < 0.1f)
        {
            // At the exact body position: pick a random direction.
            float randomAngle = (float)(_random.NextDouble() * Math.PI * 2);
            away = new Vector2((float)Math.Cos(randomAngle), (float)Math.Sin(randomAngle));
        }
        else
        {
            away = Vector2.Normalize(away);
        }

        location = body + away * requiredDistance;
        return true;
    }

    /// <summary>
    /// A random safe location between the asteroid belt outer radius and the system radius.
    /// </summary>
    public Vector2 GetRandomSafeLocationOutsideAsteroidBelt()
    {
        float inner = _system.AsteroidBeltOuterRadius;
        float outer = _system.SystemRadius;

        const int maxAttempts = 20;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            float angle = (float)(_random.NextDouble() * Math.PI * 2);
            float distance = inner + (float)(_random.NextDouble() * (outer - inner));

            Vector2 candidate = new Vector2((float)Math.Cos(angle) * distance, (float)Math.Sin(angle) * distance);
            Vector2 safePosition = GetSafeLocation(candidate);

            float safeDistance = safePosition.Length();
            if (safeDistance >= inner && safeDistance <= outer)
                return safePosition;
        }

        // Fallback: mid-range on a random angle
        float fallbackAngle = (float)(_random.NextDouble() * Math.PI * 2);
        float fallbackDistance = (inner + outer) / 2f;
        return new Vector2((float)Math.Cos(fallbackAngle) * fallbackDistance, (float)Math.Sin(fallbackAngle) * fallbackDistance);
    }
}
