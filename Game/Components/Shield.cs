namespace Strange_Universe.Game.Components;

/// <summary>A shield generator.</summary>
public sealed class Shield : Equipment
{
    public Shield() : base(EquipmentType.Shield) { }

    /// <summary>Energy used to run the shield.</summary>
    public float EnergyCost { get; set; }

    public float ShieldStrength { get; set; }

    /// <summary>Shield points regained per second.</summary>
    public float RechargeRate { get; set; }

    /// <summary>Seconds after taking damage before the shield starts to recharge.</summary>
    public float RechargeDelay { get; set; }
}
