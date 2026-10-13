namespace Strange_Universe.Game.Components;

/// <summary>Armor plating that adds to a ship's hull.</summary>
public sealed class HullPlating : Equipment
{
    public HullPlating() : base(EquipmentType.Hull) { }

    public float HullStrength { get; set; }
}
