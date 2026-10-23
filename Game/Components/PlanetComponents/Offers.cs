using System;
using Strange_Universe.Game.Components.EquipmentComponents;
using Strange_Universe.Game.Components.ShipComponents;

namespace Strange_Universe.Game.Components.PlanetComponents;

/// <summary>
/// A piece of equipment for sale. It holds how to build the item, so every purchase makes a new object (no two ships share equipment), and a
/// <see cref="Template"/> built once to show its name, price and mass. The template is never installed on a ship.
/// </summary>
public sealed class EquipmentOffer
{
    private readonly Func<EquipmentComponent> _build;

    public EquipmentOffer(Func<EquipmentComponent> build)
    {
        _build = build ?? throw new ArgumentNullException(nameof(build));
        Template = build();
    }

    /// <summary>An example of the item, for display. Not the object a buyer gets.</summary>
    public EquipmentComponent Template { get; }

    public int Price => Template.Price;

    /// <summary>A new item for a buyer.</summary>
    public EquipmentComponent Create() => _build();
}

/// <summary>
/// A ship for sale, which comes new with its default loadout. Like <see cref="EquipmentOffer"/> it builds a new ship type for each buyer and keeps a
/// <see cref="Template"/> for display.
/// </summary>
public sealed class ShipOffer
{
    private readonly Func<ShipStats> _build;

    public ShipOffer(Func<ShipStats> build)
    {
        _build = build ?? throw new ArgumentNullException(nameof(build));
        Template = build();
    }

    /// <summary>An example of the ship type, for display. Not the object a buyer gets.</summary>
    public ShipStats Template { get; }

    /// <summary>What the ship sells for: the ship plus the equipment it comes with (before any trade-in).</summary>
    public int ListPrice => Template.ListPrice;

    /// <summary>A new ship type for a buyer, with its own default loadout.</summary>
    public ShipStats Create() => _build();
}
