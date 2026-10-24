using Strange_Universe.Game.Components.ShipComponents;
namespace Strange_Universe.Game.Components.EquipmentComponents;

/// <summary>An engine: thrust and fuel capacity.</summary>
public sealed class EngineComponent : EquipmentComponent
{
    /// <summary>The ship type's limit for this kind of equipment.</summary>
    public override int SlotLimit(LoadoutLimits limits) => limits.MaxEngines;

    public EngineComponent() : base(EquipmentType.Engine) { }

    public float EngineThrust { get; set; }

    public float FuelLevel { get; set; }
}
