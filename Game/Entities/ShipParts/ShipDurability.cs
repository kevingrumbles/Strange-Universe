using System;
using System.Collections.Generic;
using System.Linq;
using Strange_Universe.Game.Components;

namespace Strange_Universe.Game.Entities.ShipParts;

/// <summary>Hull, shield and fuel state plus damage application.</summary>
public class ShipDurability
{
    private readonly Func<DurabilityStats> _stats;

    private readonly Func<IEnumerable<Equipment>> _equipment;
    private bool _hasShield;
    private int? _requested;   // a total set from outside, handed out to the generators the next time the shield is used

    /// <param name="stats">The ship type's durability numbers.</param>
    /// <param name="equipment">The ship's equipment (null for none). Shields and hull plating add to the ship type's base values; each shield generator holds and recharges its own strength.</param>
    public ShipDurability(Func<DurabilityStats> stats, Func<IEnumerable<Equipment>> equipment = null)
    {
        _stats = stats ?? throw new ArgumentNullException(nameof(stats));
        _equipment = equipment;
    }

    public int? Hull   { get; set; }
    public int? Fuel   { get; set; }

    private IEnumerable<Equipment> Carried => _equipment?.Invoke() ?? Enumerable.Empty<Equipment>();
    private List<Shield> Generators => Carried.OfType<Shield>().ToList();

    /// <summary>
    /// The ship's total shield: what each generator has left. Setting it fills the generators in order (any excess is dropped). (The strength lives in the generators; this total is what is saved.)
    /// </summary>
    public int? Shield
    {
        get
        {
            if (!_hasShield)
                return null;
            HandOutRequested();
            return (int)Math.Floor(Generators.Sum(g => g.Strength) + 0.001f);
        }
        set
        {
            _hasShield = value.HasValue;
            _requested = value;
        }
    }

    /// <summary>The ship type's hull plus the strength of every hull plating it carries.</summary>
    public int MaxHull   => _stats().MaxHull + (int)Carried.OfType<HullPlating>().Sum(h => h.HullStrength);

    /// <summary>The strength of every shield generator the ship carries; ships get a shield only from equipment.</summary>
    public int MaxShield => (int)Carried.OfType<Shield>().Sum(g => g.ShieldStrength);
    public int MaxFuel   => _stats().MaxFuel;

    public float HullPercentage   => Percent(Hull, MaxHull);
    public float ShieldPercentage => Percent(Shield, MaxShield);
    public float FuelPercentage   => Percent(Fuel, MaxFuel);

    /// <summary>True once the hull has been fully depleted.</summary>
    public bool IsDestroyed => Hull is <= 0;

    /// <summary>The most recent ship to damage this one. Used for kill attribution.</summary>
    public Ship LastDamageSource { get; private set; }

    /// <summary>
    /// Applies a hit. The first shield that is still up (the generators in order, then the ship type's own shield) absorbs
    /// all of it, and no damage spills over to the next shield or the hull. When every shield is down the hull takes it.
    /// Any hit restarts the recharge delay of every shield generator, whichever one took it.
    /// </summary>
    public void ApplyDamage(int damage, Ship source = null)
    {
        if (damage <= 0 || IsDestroyed)
            return;

        bool absorbed = false;
        if (_hasShield)
        {
            HandOutRequested();
            var generators = Generators;

            var active = generators.FirstOrDefault(g => !g.IsDown);
            if (active != null)
            {
                active.Absorb(damage);
                absorbed = true;
            }

            foreach (var generator in generators)
                generator.RestartRechargeDelay();
        }

        if (!absorbed && Hull.HasValue)
            Hull = Math.Max(0, Hull.Value - damage);

        LastDamageSource = source ?? LastDamageSource;
    }

    /// <summary>Lets every shield generator recharge. Each follows its own rate and delay.</summary>
    public void Update(float deltaTime)
    {
        if (IsDestroyed || !_hasShield || deltaTime <= 0f)
            return;

        HandOutRequested();
        foreach (var generator in Generators)
            generator.Update(deltaTime);
    }

    private void HandOutRequested()
    {
        if (_requested is not { } total)
            return;
        _requested = null;

        float left = total;
        foreach (var generator in Generators)
        {
            generator.SetStrength(left);
            left -= generator.Strength;
        }
    }

    private static float Percent(int? current, int max)
        => current.HasValue && max > 0 ? (float)current.Value / max * 100f : 0f;
}
