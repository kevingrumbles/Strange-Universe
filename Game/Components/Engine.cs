namespace Strange_Universe.Game.Components;

/// <summary>An engine: thrust and fuel capacity.</summary>
public sealed class Engine : Equipment
{
    public Engine() : base(EquipmentType.Engine) { }

    public float EngineThrust { get; set; }

    public float FuelLevel { get; set; }
}
