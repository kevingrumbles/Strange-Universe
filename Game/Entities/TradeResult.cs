namespace Strange_Universe.Game.Entities;

/// <summary>How a purchase or a sale at a spaceport went. Anything but <see cref="Ok"/> changed nothing.</summary>
public enum TradeResult
{
    Ok,

    /// <summary>The player is not docked, so there is no spaceport to trade at.</summary>
    NotDocked,

    NotEnoughCredits,

    /// <summary>The ship has no free slot for that kind of equipment (see <see cref="ShipParts.InstallCheck.NoSlot"/>).</summary>
    NoSlot,

    /// <summary>The equipment would take the ship over its mass limit (see <see cref="ShipParts.InstallCheck.TooHeavy"/>).</summary>
    TooHeavy,

    /// <summary>The item to sell is not on the ship.</summary>
    NotCarried,
}
