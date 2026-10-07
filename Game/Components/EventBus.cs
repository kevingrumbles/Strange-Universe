using System;
using System.Collections.Generic;
using Strange_Universe.Game.Entities;

namespace Strange_Universe.Game.Components;

/// <summary>In-process publish/subscribe for simulation events. Single-threaded: publish and subscribe on the main thread.</summary>
public interface IEventBus
{
    /// <summary>Calls <paramref name="handler"/> for every published <typeparamref name="T"/> until the returned subscription is disposed.</summary>
    IDisposable Subscribe<T>(Action<T> handler);

    void Publish<T>(T evt);
}

public sealed class EventBus : IEventBus
{
    private readonly Dictionary<Type, List<Delegate>> _handlers = new();

    public IDisposable Subscribe<T>(Action<T> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        if (!_handlers.TryGetValue(typeof(T), out var list))
            _handlers[typeof(T)] = list = new List<Delegate>();
        list.Add(handler);
        return new Subscription(() => list.Remove(handler));
    }

    public void Publish<T>(T evt)
    {
        if (!_handlers.TryGetValue(typeof(T), out var list) || list.Count == 0) return;

        // Snapshot so handlers may unsubscribe (or subscribe) while handling.
        foreach (var handler in list.ToArray())
            ((Action<T>)handler)(evt);
    }

    private sealed class Subscription : IDisposable
    {
        private Action _remove;
        public Subscription(Action remove) => _remove = remove;
        public void Dispose() { _remove?.Invoke(); _remove = null; }
    }
}

/// <summary>Discards everything. For code that runs without a universe.</summary>
public sealed class NullEventBus : IEventBus
{
    public static readonly NullEventBus Instance = new();
    public IDisposable Subscribe<T>(Action<T> handler) => new NoSubscription();
    public void Publish<T>(T evt) { }
    private sealed class NoSubscription : IDisposable { public void Dispose() { } }
}

/// <summary>A projectile damaged a ship.</summary>
public readonly record struct ShipDamaged(Ship Ship, Projectile Projectile, bool ShieldWasUp);

/// <summary>A ship's hull reached zero. Published once per ship.</summary>
public readonly record struct ShipDestroyed(Ship Ship, Ship Killer);

/// <summary>A projectile struck a ship or an asteroid (exactly one of the targets is set).</summary>
public readonly record struct ProjectileHit(Projectile Projectile, Ship TargetShip, Asteroid TargetAsteroid);

/// <summary>The player entered a star system; it is now the active one.</summary>
public readonly record struct SystemEntered(StarSystem System);

/// <summary>The simulation wants a short on-screen message shown.</summary>
public readonly record struct MessageRequested(string Message, int DurationSeconds);
