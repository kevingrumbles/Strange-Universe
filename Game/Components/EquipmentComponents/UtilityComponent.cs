namespace Strange_Universe.Game.Components.EquipmentComponents;

/// <summary>
/// General-purpose gear. Besides any effect of its own, it can improve the ship's weapons: the bonuses of all utility
/// modules on a ship are added together and applied to every weapon it fires (see <c>WeaponSystem</c>).
/// </summary>
public sealed class UtilityComponent : EquipmentComponent
{
    /// <summary>The ship type's limit for this kind of equipment.</summary>
    public override int SlotLimit(Strange_Universe.Game.Components.ShipComponents.LoadoutLimits limits) => limits.MaxUtilities;

    public UtilityComponent() : base(EquipmentType.Utility) { }

    /// <summary>Energy used to run the module.</summary>
    public float EnergyCost { get; set; }

    /// <summary>Added to every weapon's shots per second.</summary>
    public float AttackSpeedBonus { get; set; }

    /// <summary>Added to every weapon's range.</summary>
    public float AttackRangeBonus { get; set; }

    /// <summary>Added to every weapon's accuracy.</summary>
    public float AccuracyBonus { get; set; }
}
