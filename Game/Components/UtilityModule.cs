namespace Strange_Universe.Game.Components;

/// <summary>
/// General-purpose gear. Besides any effect of its own, it can improve the ship's weapons: the bonuses of all utility
/// modules on a ship are added together and applied to every weapon it fires (see <c>WeaponSystem</c>).
/// </summary>
public sealed class UtilityModule : Equipment
{
    public UtilityModule() : base(EquipmentType.Utility) { }

    /// <summary>Added to every weapon's shots per second.</summary>
    public float AttackSpeedBonus { get; set; }

    /// <summary>Added to every weapon's range.</summary>
    public float AttackRangeBonus { get; set; }

    /// <summary>Added to every weapon's accuracy.</summary>
    public float AccuracyBonus { get; set; }
}
