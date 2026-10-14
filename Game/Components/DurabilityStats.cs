namespace Strange_Universe.Game.Components;

/// <summary>What a type of ship has on its own, before equipment: its hull and fuel. Shield, and extra hull, come from equipment.</summary>
public sealed class DurabilityStats
{
    public int MaxHull { get; set; }
    public int MaxFuel { get; set; }
}
