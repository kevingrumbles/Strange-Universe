using System;

namespace Strange_Universe.Game.Components;

/// <summary>
/// A shield generator. It holds its own strength and does its own recharging, so a ship can carry several
/// generators, each with its own strength, recharge rate and delay.
/// </summary>
public sealed class Shield : Equipment
{
    private float? _strength; // null until it is first used: a new generator is at full strength
    private float _rechargeDelayLeft;

    public Shield() : base(EquipmentType.Shield) { }

    /// <summary>Energy used to run the shield.</summary>
    public float EnergyCost { get; set; }

    /// <summary>The most this generator can hold.</summary>
    public float ShieldStrength { get; set; }

    /// <summary>Shield points regained per second.</summary>
    public float RechargeRate { get; set; }

    /// <summary>Seconds after the shield goes down before it starts to recharge.</summary>
    public float RechargeDelay { get; set; }

    /// <summary>How much shield this generator has left.</summary>
    public float Strength => _strength ?? ShieldStrength;

    /// <summary>True when the generator has nothing left.</summary>
    public bool IsDown => Strength <= 0f;

    /// <summary>Seconds left before a downed shield starts to recharge; 0 when it is not waiting.</summary>
    public float RechargeDelayRemaining => _rechargeDelayLeft;

    /// <summary>Sets the strength directly (clamped to 0..<see cref="ShieldStrength"/>) without starting a recharge delay.</summary>
    internal void SetStrength(float strength) => _strength = Math.Clamp(strength, 0f, ShieldStrength);

    /// <summary>
    /// Takes a hit. An active shield absorbs the whole hit: it loses the damage, down to 0, and none of it spills over.
    /// </summary>
    public void Absorb(float damage)
    {
        if (damage > 0f)
            _strength = Math.Max(0f, Strength - damage);
    }

    /// <summary>Starts the recharge delay over. Every hit the ship takes does this to every generator.</summary>
    public void RestartRechargeDelay() => _rechargeDelayLeft = RechargeDelay;

    /// <summary>Advances the recharge: waits out the delay, then regains <see cref="RechargeRate"/> per second up to the maximum.</summary>
    public void Update(float deltaTime)
    {
        if (deltaTime <= 0f)
            return;

        if (_rechargeDelayLeft > 0f)
        {
            float waited = Math.Min(_rechargeDelayLeft, deltaTime);
            _rechargeDelayLeft -= waited;
            deltaTime -= waited; // time left over after the delay counts towards recharging
        }

        if (deltaTime > 0f && RechargeRate > 0f && Strength < ShieldStrength)
            SetStrength(Strength + RechargeRate * deltaTime);
    }
}
