namespace Strange_Universe.Game.Components.EquipmentComponents;

/// <summary>
/// The kinds of equipment a ship type limits (see <see cref="ShipComponents.LoadoutLimits"/>). Every kind of weapon, missiles included, takes a
/// weapon slot. An item that is no longer a known item has no slot kind.
/// </summary>
public enum SlotKind
{
    Weapon,
    Shield,
    Plating,
    Engine,
    Utility,
}
