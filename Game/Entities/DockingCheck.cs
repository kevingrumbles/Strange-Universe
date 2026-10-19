namespace Strange_Universe.Game.Entities;

/// <summary>Whether a ship can dock at a planet right now, and if not, why not. Decided by <see cref="Planet.CheckDocking"/>.</summary>
public enum DockingCheck
{
    /// <summary>In range and nearly stopped: the ship can dock.</summary>
    Ok,

    /// <summary>The planet has no spaceport.</summary>
    NoSpaceport,

    /// <summary>Further from the planet's surface than <see cref="Planet.DockRange"/>.</summary>
    OutOfRange,

    /// <summary>In range, but moving faster than <see cref="Planet.MaxDockSpeed"/>.</summary>
    TooFast,
}
