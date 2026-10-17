using System;
using Strange_Universe.Game.Components;
using Strange_Universe.Game.Entities.ShipParts;

namespace Strange_Universe.Game.Screens;

/// <summary>Watches a universe's events and notes when its player has been destroyed.</summary>
public sealed class PlayerDeathMonitor : IDisposable
{
    private readonly IDisposable _subscription;

    public PlayerDeathMonitor(IEventBus events, Player player)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(player);

        PlayerDestroyed = player.IsDestroyed;
        _subscription = events.Subscribe<ShipDestroyed>(e =>
        {
            if (ReferenceEquals(e.Ship, player))
                PlayerDestroyed = true;
        });
    }

    /// <summary>True once the player's hull has reached zero.</summary>
    public bool PlayerDestroyed { get; private set; }

    public void Dispose() => _subscription.Dispose();
}
