using System;
using Strange_Universe.Game.Components;

namespace Strange_Universe.Game.Entities.ShipParts;

/// <summary>Hull, shield and fuel state plus damage application.</summary>
public class ShipDurability
{
    private readonly Func<ShipStats> _stats;

    public ShipDurability(Func<ShipStats> stats)
    {
        _stats = stats ?? throw new ArgumentNullException(nameof(stats));
    }

    public int? Hull   { get; set; }
    public int? Shield { get; set; }
    public int? Fuel   { get; set; }

    public int MaxHull   => _stats().MaxHull;
    public int MaxShield => _stats().MaxShield;
    public int MaxFuel   => _stats().MaxFuel;

    public float HullPercentage   => Percent(Hull, MaxHull);
    public float ShieldPercentage => Percent(Shield, MaxShield);
    public float FuelPercentage   => Percent(Fuel, MaxFuel);

    /// <summary>True once the hull has been fully depleted.</summary>
    public bool IsDestroyed => Hull is <= 0;

    /// <summary>The most recent ship to damage this one. Used for kill attribution.</summary>
    public Ship LastDamageSource { get; private set; }

    /// <summary>Applies raw damage, depleting shields first and then hull (never below zero).</summary>
    public void ApplyDamage(int damage, Ship source = null)
    {
        if (damage <= 0 || IsDestroyed)
            return;

        int remaining = damage;

        if (Shield is > 0)
        {
            int absorbed = Math.Min(Shield.Value, remaining);
            Shield    -= absorbed;
            remaining -= absorbed;
        }

        if (remaining > 0 && Hull.HasValue)
            Hull = Math.Max(0, Hull.Value - remaining);

        LastDamageSource = source;
    }

    private static float Percent(int? current, int max)
        => current.HasValue && max > 0 ? (float)current.Value / max * 100f : 0f;
}
