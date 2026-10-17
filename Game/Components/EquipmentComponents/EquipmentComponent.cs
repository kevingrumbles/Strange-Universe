using System;

namespace Strange_Universe.Game.Components.EquipmentComponents;

/// <summary>What kind of equipment an item is. For weapons it also says how the weapon works.</summary>
public enum EquipmentType
{
    FixedProjectile,
    TurretProjectile,
    FixedBeam,
    TurretBeam,
    FixedMissile,
    TurretMissile,
    Shield,
    Hull,
    Engine,
    Utility,
    Unkown
}

/// <summary>
/// Something a ship can carry. Each kind of equipment is its own subclass holding only the stats that apply to it:
/// <see cref="Weapon"/>, <see cref="ShieldComponent"/>, <see cref="HullPlating"/>, <see cref="Engine"/> and
/// <see cref="UtilityComponent"/>; <see cref="UnknownComponent"/> stands in for a name that is no longer a known item.
/// <para>
/// Equipment is not purely data (a weapon has a cooldown), so every ship holds its own copy. Get one with
/// <see cref="Create"/>. Only the name is saved; the full item is restored from it when a save is loaded.
/// </para>
/// </summary>
public abstract class EquipmentComponent
{
    public string EquipmentName { get; set; }

    public EquipmentType EquipmentType { get; private protected set; }

    public float Mass { get; set; }

    private protected EquipmentComponent(EquipmentType type)
    {
        EquipmentType = type;
    }

    /// <summary>
    /// A new equipment item for the item called <paramref name="equipmentName"/> (names are not case sensitive). Every call
    /// returns a separate object, so nothing is shared between ships. A name that is not a known item (one that no longer
    /// exists, say) gives an inert <see cref="UnknownComponent"/> that keeps its name, so it is written back out unchanged.
    /// </summary>
    public static EquipmentComponent Create(string equipmentName)
        => EquipmentCatalog.Create(equipmentName) ?? new UnknownComponent(equipmentName);
}
