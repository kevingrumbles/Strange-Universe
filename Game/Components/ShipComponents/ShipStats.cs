using System;
using System.Collections.Generic;
using System.Linq;
using Strange_Universe.Game.Components.EquipmentComponents;
using Strange_Universe.Game.Entities.ShipParts;

namespace Strange_Universe.Game.Components.ShipComponents;

/// <summary>
/// A type of ship. Each type is its own subclass (<see cref="Shuttle"/>, <see cref="Runner"/>, ...) so types can
/// grow their own differences. What every type has is split into small parts: <see cref="Flight"/> (how it moves),
/// <see cref="Durability"/> (hull and fuel), <see cref="Appearance"/> (its art) and the <see cref="DefaultLoadout"/> it starts with.
/// <para>
/// Make a type with <c>new Shuttle()</c>: each one has its own loadout objects, so a type object belongs to the one ship it is given to.
/// Give it to a ship with <see cref="Ship.AssignShipType"/> (or a ship's constructor); the ship starts with the default loadout and can then change it.
/// Only <see cref="ShipTypeName"/> is saved (see <see cref="ShipStatsConverter"/>).
/// </para>
/// </summary>
public abstract class ShipStats
{
    /// <summary>The type's name, which is what is saved and what its art is registered under.</summary>
    public abstract string ShipTypeName { get; }

    public FlightStats Flight { get; } = new();
    public DurabilityStats Durability { get; } = new();
    public ShipAppearance Appearance { get; } = new();

    /// <summary>
    /// About how many credits an NPC of this type carries. Each NPC has its own amount, between half and one and a half times this
    /// (see <see cref="Entities.ShipParts.Nonplayer"/>).
    /// </summary>
    public int TypicalCredits { get; protected set; }

    /// <summary>How advanced the ship is, 1 and up. Spaceports list ships by grade, the highest last.</summary>
    public int Grade { get; protected set; }

    /// <summary>What the ship itself costs, without any equipment. See <see cref="ListPrice"/> for what it sells for new.</summary>
    public int Price { get; protected set; }

    /// <summary>What a new ship of this type sells for: the ship plus the equipment it starts with.</summary>
    public int ListPrice => Price + DefaultLoadout.Sum(item => item.Price);

    /// <summary>How much equipment a ship of this type can carry.</summary>
    public LoadoutLimits Limits { get; } = new();

    /// <summary>The equipment a ship of this type starts with. These are the objects the ship is given, so they are never shared between ships.</summary>
    public IReadOnlyList<EquipmentComponent> DefaultLoadout { get; protected set; } = Array.Empty<EquipmentComponent>();

    /// <summary>
    /// A new instance of the built-in type with this name (not case sensitive), falling back to the Shuttle for an unknown name.
    /// Only the name is saved, so this is used by <see cref="ShipStatsConverter"/> to restore the type when a save is loaded.
    /// </summary>
    internal static ShipStats FromName(string shipTypeName)
        => BuiltIn().FirstOrDefault(type => string.Equals(type.ShipTypeName, shipTypeName, StringComparison.OrdinalIgnoreCase)) ?? new Shuttle();

    /// <summary>A new instance of every built-in ship type (the first is the Shuttle). Add each new type here.</summary>
    internal static IReadOnlyList<ShipStats> BuiltIn() => new ShipStats[] { new Shuttle(), new Runner() };

    /// <summary>
    /// A new ship type of the same kind, with its own parts and its own new default loadout. A spaceport keeps one of each ship type it sells and hands
    /// each buyer a copy.
    /// </summary>
    public ShipStats Clone() => (ShipStats)Activator.CreateInstance(GetType());

    /// <summary>Drops the default loadout, for a ship whose equipment comes from a save instead.</summary>
    internal void ClearDefaultLoadout() => DefaultLoadout = Array.Empty<EquipmentComponent>();
}
