using System.Collections.Generic;

namespace Strange_Universe.Game.Entities;

/// <summary>
/// Everything <see cref="StarSystemGenerator"/> derives from a node: scalar parameters and the initial
/// entities. Immutable data produced without touching the universe, galaxy or any asset service; the
/// same node always yields an equal layout. The entity instances are handed over to the
/// <see cref="StarSystem"/> built from the layout, so a layout is consumed by one system.
/// </summary>
public sealed record StarSystemLayout
{
    public int   PlanetCount             { get; init; }
    public int   AsteroidCount           { get; init; }
    public int   StarCount               { get; init; }
    public float SystemRadius            { get; init; }
    public float MandevilleRadius        { get; init; }
    public float StarOrbitRadius         { get; init; }
    public float StarOrbitSpeed          { get; init; }
    public float AsteroidBeltInnerRadius { get; init; }
    public float AsteroidBeltOuterRadius { get; init; }
    public int   BackgroundStarCount     { get; init; }
    public float StarRadius              { get; init; }
    public float MinPlanetRadius         { get; init; }
    public float MaxPlanetRadius         { get; init; }
    public float MinAsteroidRadius       { get; init; }
    public float MaxAsteroidRadius       { get; init; }
    public int   SystemConnectionCount   { get; init; } = 1;
    public int   InnerPlanetCount        { get; init; }

    /// <summary>Slot of the universe's nebula pool this system uses.</summary>
    public int NebulaIndex { get; init; }

    public IReadOnlyList<Star>           Stars           { get; init; } = new List<Star>();
    public IReadOnlyList<Planet>         Planets         { get; init; } = new List<Planet>();
    public IReadOnlyList<Asteroid>       Asteroids       { get; init; } = new List<Asteroid>();
    public IReadOnlyList<BackgroundStar> BackgroundStars { get; init; } = new List<BackgroundStar>();
}
