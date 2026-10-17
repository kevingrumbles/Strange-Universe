namespace Strange_Universe.Game.Components.EquipmentComponents;

/// <summary>An engine: thrust and fuel capacity.</summary>
public sealed class EngineComponent : EquipmentComponent
{
    public EngineComponent() : base(EquipmentType.Engine) { }

    public float EngineThrust { get; set; }

    public float FuelLevel { get; set; }
}
