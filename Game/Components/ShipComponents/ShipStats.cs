using System;
using System.Collections.Generic;
using Strange_Universe.Game.Components.EquipmentComponents;

namespace Strange_Universe.Game.Components.ShipComponents;

/// <summary>
/// A type of ship. Each type is its own subclass (<see cref="Shuttle"/>, <see cref="Runner"/>, ...) so types can
/// grow their own differences. What every type has is split into small parts: <see cref="Flight"/> (how it moves),
/// <see cref="Durability"/> (hull and fuel), <see cref="Appearance"/> (its art) and the <see cref="DefaultLoadout"/> it starts with.
/// <para>
/// Make a type with <c>new Shuttle()</c>: each one has its own loadout objects, so a type object belongs to the one ship it is given to.
/// Give it to a ship with <see cref="Entities.Ship.AssignShipType"/> (or a ship's constructor); the ship starts with the default loadout and can then change it.
/// Only <see cref="ShipTypeName"/> is saved (see <see cref="ShipStatsConverter"/>).
/// </para>
/// </summary>
public abstract class ShipStats
{
    /// <summary>Test hook: runs on the flight stats of every new type, so a test run can try different tuning without editing the game's numbers.</summary>
    internal static Action<FlightStats> FlightTuning { get; set; }

    /// <summary>The type's name, which is what is saved and what its art is registered under.</summary>
    public abstract string ShipTypeName { get; }

    public FlightStats Flight { get; } = new();
    public DurabilityStats Durability { get; } = new();
    public ShipAppearance Appearance { get; } = new();

    /// <summary>The equipment a ship of this type starts with. These are the objects the ship is given, so they are never shared between ships.</summary>
    public IReadOnlyList<EquipmentComponent> DefaultLoadout { get; protected set; } = Array.Empty<EquipmentComponent>();

    /// <summary>Subclasses call this last in their constructor, after setting their values.</summary>
    protected void FinishSetup() => FlightTuning?.Invoke(Flight);

    /// <summary>
    /// A new instance of the built-in type with this name (not case sensitive), falling back to the Shuttle for an unknown name.
    /// Only the name is saved, so this is used by <see cref="ShipStatsConverter"/> to restore the type when a save is loaded.
    /// </summary>
    internal static ShipStats FromName(string shipTypeName)
        => string.Equals(shipTypeName, "Runner", StringComparison.OrdinalIgnoreCase) ? new Runner() : new Shuttle();

    /// <summary>Drops the default loadout, for a ship whose equipment comes from a save instead.</summary>
    internal void ClearDefaultLoadout() => DefaultLoadout = Array.Empty<EquipmentComponent>();
}
