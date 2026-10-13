using System;
using Strange_Universe.Game.Components;

namespace Strange_Universe.Game.Entities.ShipParts;

/// <summary>Hull, shield and fuel state plus damage application.</summary>
public class ShipDurability
{
    private readonly Func<ShipStats> _stats;

    private readonly Func<Shield> _shieldGenerator;
    private float _rechargeDelayLeft;
    private float _rechargeRemainder; // the fraction of a shield point not yet added

    /// <param name="stats">The ship's current stats.</param>
    /// <param name="shieldGenerator">The shield generator the ship carries (null when it has none); it sets how the shield recharges.</param>
    public ShipDurability(Func<ShipStats> stats, Func<Shield> shieldGenerator = null)
    {
        _stats = stats ?? throw new ArgumentNullException(nameof(stats));
        _shieldGenerator = shieldGenerator;
    }

    /// <summary>Seconds left before a downed shield starts to recharge; 0 when it is not waiting.</summary>
    public float RechargeDelayRemaining => _rechargeDelayLeft;

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

            // The shield just went down: it stays off for the generator's recharge delay.
            if (Shield == 0)
            {
                _rechargeDelayLeft = _shieldGenerator?.Invoke()?.RechargeDelay ?? 0f;
                _rechargeRemainder = 0f;
            }
        }

        if (remaining > 0 && Hull.HasValue)
            Hull = Math.Max(0, Hull.Value - remaining);

        LastDamageSource = source;
    }

    /// <summary>
    /// Recharges the shield. Once the delay that began when the shield went down has passed, the shield regains the
    /// generator's recharge rate per second, up to the maximum. A ship with no shield generator does not recharge.
    /// </summary>
    public void Update(float deltaTime)
    {
        if (IsDestroyed || Shield is null || deltaTime <= 0f)
            return;

        if (_rechargeDelayLeft > 0f)
        {
            float waited = Math.Min(_rechargeDelayLeft, deltaTime);
            _rechargeDelayLeft -= waited;
            deltaTime -= waited; // time left over after the delay counts towards recharging
        }

        var generator = _shieldGenerator?.Invoke();
        if (deltaTime <= 0f || generator == null || generator.RechargeRate <= 0f || Shield >= MaxShield)
            return;

        _rechargeRemainder += generator.RechargeRate * deltaTime;
        int whole = (int)_rechargeRemainder;
        _rechargeRemainder -= whole;
        Shield = Math.Min(MaxShield, Shield.Value + whole);
        if (Shield >= MaxShield)
            _rechargeRemainder = 0f;
    }

    private static float Percent(int? current, int max)
        => current.HasValue && max > 0 ? (float)current.Value / max * 100f : 0f;
}
