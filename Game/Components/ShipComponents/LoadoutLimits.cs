using System;
using Strange_Universe.Game.Components.EquipmentComponents;

namespace Strange_Universe.Game.Components.ShipComponents;

/// <summary>
/// How much equipment a type of ship can carry: the most items of each <see cref="SlotKind"/>, and the most mass of equipment in all.
/// The limits are checked when equipment is installed (<see cref="Entities.ShipParts.Ship.CheckInstall"/>), never on a ship that already has more
/// (a default loadout or an old save is not trimmed).
/// </summary>
public sealed class LoadoutLimits
{
    public int MaxWeapons   { get; set; }
    public int MaxShields   { get; set; }
    public int MaxPlating   { get; set; }
    public int MaxEngines   { get; set; }
    public int MaxUtilities { get; set; }

    /// <summary>The most total <see cref="EquipmentComponent.Mass"/> of equipment the ship can carry.</summary>
    public float MaxMass { get; set; }

    /// <summary>The most items of <paramref name="kind"/>.</summary>
    public int MaxOf(SlotKind kind) => kind switch
    {
        SlotKind.Weapon  => MaxWeapons,
        SlotKind.Shield  => MaxShields,
        SlotKind.Plating => MaxPlating,
        SlotKind.Engine  => MaxEngines,
        SlotKind.Utility => MaxUtilities,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}
