using System;
using System.Collections.Generic;
using System.Linq;
using Strange_Universe.Game.Components.EquipmentComponents;
using Strange_Universe.Game.Components.ShipComponents;
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

    /// <summary>Carries the service out for the docked player. A store (<see cref="OpensRows"/>) does nothing here.</summary>
    public virtual void Perform(Universe universe) { }

    /// <summary>
    /// True for a service that opens a list when chosen (a store) instead of doing something at once. The docked menu then shows its
    /// <see cref="Rows"/> and passes the player's choice to <see cref="Choose"/>.
    /// </summary>
    public virtual bool OpensRows => false;

    /// <summary>The lines of the list this service opens, as they are now.</summary>
    public virtual IReadOnlyList<string> Rows(Universe universe) => Array.Empty<string>();

    /// <summary>The player picked row <paramref name="row"/> of <see cref="Rows"/>.</summary>
    public virtual void Choose(Universe universe, int row) { }
}

/// <summary>
/// Sells equipment. It keeps a list of one item of each kind it sells (<see cref="Stock"/>); a buyer is given a copy (<see cref="EquipmentComponent.Clone"/>),
/// so the stock never runs out. After the items for sale the list offers each item the player carries, to sell back.
/// </summary>
public sealed class EquipmentBayService : SpaceportService
{
    public EquipmentBayService(IEnumerable<EquipmentComponent> stock) => Stock = Ordered(stock);

    /// <summary>What is for sale, by kind of equipment and then by grade, the highest last. These are display copies; what a buyer gets is a clone.</summary>
    public List<EquipmentComponent> Stock { get; }

    /// <summary>The order equipment is listed in: by <see cref="EquipmentComponent.EquipmentType"/>, then <see cref="EquipmentComponent.Grade"/>, then price.</summary>
    private static List<EquipmentComponent> Ordered(IEnumerable<EquipmentComponent> items) =>
        items.OrderBy(item => item.EquipmentType).ThenBy(item => item.Grade).ThenBy(item => item.Price).ToList();

    public override string Name => "Equipment";
    public override bool OpensRows => true;

    /// <summary>The player's items that can be sold back (anything that is still a known item).</summary>
    private static List<EquipmentComponent> Sellable(Universe universe) =>
        Ordered(universe.Player.Equipment.Where(item => item is not UnknownComponent));

    public override IReadOnlyList<string> Rows(Universe universe)
    {
        var limits = universe.Player.ShipType.Limits;
        var rows = new List<string>();

        foreach (var item in Stock)
        {
            rows.Add($"{item.EquipmentName}   G{item.Grade}   {item.Price} cr   mass {item.Mass:0}   [{universe.Player.CountLike(item)}/{item.SlotLimit(limits)}]");
        }
        foreach (var item in Sellable(universe))
            rows.Add($"Sell {item.EquipmentName}   {item.Price} cr");

        return rows;
    }

    public override void Choose(Universe universe, int row)
    {
        if (row < 0) return;
        if (row < Stock.Count)
        {
            universe.BuyEquipment(Stock[row].Clone());
            return;
        }

        var sellable = Sellable(universe);
        int sell = row - Stock.Count;
        if (sell < sellable.Count)
            universe.SellEquipment(sellable[sell]);
    }
}

/// <summary>
/// Sells ships. It keeps a list of one ship type of each kind it sells (<see cref="Stock"/>); a buyer is given a clone. The player's current ship
/// and everything on it is traded in against the price (<see cref="Universe.BuyShip"/>).
/// </summary>
public sealed class StarshipBayService : SpaceportService
{
    public StarshipBayService(IEnumerable<ShipStats> stock) => Stock = stock.OrderBy(type => type.Grade).ThenBy(type => type.ListPrice).ToList();

    /// <summary>What is for sale, by grade, the highest last. These are display copies; what a buyer gets is a clone.</summary>
    public List<ShipStats> Stock { get; }

    public override string Name => "Starships";
    public override bool OpensRows => true;

    public override IReadOnlyList<string> Rows(Universe universe) =>
        Stock.Select(type =>
            $"{type.ShipTypeName}   G{type.Grade}   {type.ListPrice} cr   trade-in {universe.Player.ShipValue} cr   cost {universe.ShipCostAfterTradeIn(type)} cr")
             .ToList();

    public override void Choose(Universe universe, int row)
    {
        if (row >= 0 && row < Stock.Count)
            universe.BuyShip(Stock[row].Clone());
    }
}

/// <summary>Fills the player's fuel. Listed only while the tank is not full.</summary>
public sealed class RefuelService : SpaceportService
{
    public override string Name => "Refuel";

    public override bool IsAvailable(Universe universe) => (universe.Player.CurrentFuelLevel ?? 0) < universe.Player.MaxFuelLevel;

    public override void Perform(Universe universe)
    {
        universe.Player.CurrentFuelLevel = universe.Player.MaxFuelLevel;
        universe.Post("Refuelled");
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
        universe.Post("Hull repaired");
    }
}
