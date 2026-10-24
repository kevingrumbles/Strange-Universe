using System.Collections.Generic;

namespace Strange_Universe.Game.Components.PlanetComponents;

/// <summary>
/// What a planet offers a docked ship. It is plain data held by <see cref="Entities.Planet.Spaceport"/> (a planet without one cannot be
/// docked at) and has no name of its own: the planet's name is used. It decides nothing about docking; that is the planet's check.
/// </summary>
public sealed class Spaceport
{
    public Spaceport(string description, IReadOnlyList<SpaceportService> services)
    {
        Description = description;
        Services = services;
    }

    /// <summary>A line or two about the place, shown when docked.</summary>
    public string Description { get; }

    /// <summary>The services on offer, in the order the docked menu lists them.</summary>
    public IReadOnlyList<SpaceportService> Services { get; }
}
