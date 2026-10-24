using Strange_Universe.Game.Components.ShipComponents;
namespace Strange_Universe.Game.Components.EquipmentComponents;

/// <summary>Armor plating that adds to a ship's hull.</summary>
public sealed class PlatingComponent : EquipmentComponent
{
    /// <summary>The ship type's limit for this kind of equipment.</summary>
    public override int SlotLimit(LoadoutLimits limits) => limits.MaxPlating;

    public PlatingComponent() : base(EquipmentType.Hull) { }

    public float HullStrength { get; set; }
}
