using Strange_Universe.Game.Entities;

namespace Strange_Universe.Game.Components.PlanetComponents;

/// <summary>
/// One thing a spaceport offers: refuelling, repairs, later an equipment store, a ship store, quests. Each service is its own class, so the
/// spaceport's contents grow by adding classes and the docked menu, which lists <see cref="Name"/>, does not change.
/// </summary>
public abstract class SpaceportService
{
    /// <summary>The menu entry.</summary>
    public abstract string Name { get; }

    /// <summary>
    /// Whether the docked menu lists this service right now. A service that has nothing to do for the player (a full tank, an undamaged hull)
    /// is left out; the default is always.
    /// </summary>
    public virtual bool IsAvailable(Universe universe) => true;

    /// <summary>Carries the service out for the docked player.</summary>
    public abstract void Perform(Universe universe);
}

/// <summary>Fills the player's fuel. Listed only while the tank is not full.</summary>
public sealed class RefuelService : SpaceportService
{
    public override string Name => "Refuel";

    public override bool IsAvailable(Universe universe) => (universe.Player.CurrentFuelLevel ?? 0) < universe.Player.MaxFuelLevel;

    public override void Perform(Universe universe)
    {
        universe.Player.CurrentFuelLevel = universe.Player.MaxFuelLevel;
        universe.Messages.Post("Refuelled");
    }
}

/// <summary>Leaves the spaceport and returns the player to the star system.</summary>
public sealed class UndockService : SpaceportService
{
    public override string Name => "Undock";

    public override void Perform(Universe universe) => universe.Undock();
}

/// <summary>Restores the player's hull. Listed only while the hull is damaged. Shields are not its job: they recharge to full whenever the player enters a system.</summary>
public sealed class RepairService : SpaceportService
{
    public override string Name => "Repair";

    public override bool IsAvailable(Universe universe) => (universe.Player.CurrentHullStrength ?? 0) < universe.Player.MaxHullStrength;

    public override void Perform(Universe universe)
    {
        universe.Player.CurrentHullStrength = universe.Player.MaxHullStrength;
        universe.Messages.Post("Hull repaired");
    }
}
