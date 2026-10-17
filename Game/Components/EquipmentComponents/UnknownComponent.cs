namespace Strange_Universe.Game.Components.EquipmentComponents;

/// <summary>
/// Stands in for an item name that is not a known item (one that has been removed since a save was written). It does nothing,
/// but keeps its name so the save is written back out unchanged.
/// </summary>
public sealed class UnknownComponent : EquipmentComponent
{
    public UnknownComponent(string equipmentName) : base(EquipmentType.Unkown)
    {
        EquipmentName = equipmentName;
    }
}
