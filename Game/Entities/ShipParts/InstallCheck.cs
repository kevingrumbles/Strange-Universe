namespace Strange_Universe.Game.Entities.ShipParts;

/// <summary>Whether a ship can take a piece of equipment, and if not, why not. Decided by <see cref="Ship.CheckInstall"/>.</summary>
public enum InstallCheck
{
    /// <summary>It fits.</summary>
    Ok,

    /// <summary>The ship has no free slot of that kind of equipment.</summary>
    NoSlot,

    /// <summary>It would take the ship's equipment over the most mass it can carry.</summary>
    TooHeavy,
}
