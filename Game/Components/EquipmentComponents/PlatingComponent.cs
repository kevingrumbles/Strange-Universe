namespace Strange_Universe.Game.Components.EquipmentComponents;

/// <summary>Armor plating that adds to a ship's hull.</summary>
public sealed class PlatingComponent : EquipmentComponent
{
    public PlatingComponent() : base(EquipmentType.Hull) { }

    public float HullStrength { get; set; }
}
