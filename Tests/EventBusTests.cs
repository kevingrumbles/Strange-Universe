using System.Collections.Generic;
using System.Linq;
using Strange_Universe.Game.Components;
using Strange_Universe.Game.Entities;
using Strange_Universe.Tests.Fakes;
using Xunit;

namespace Strange_Universe.Tests;

/// <summary>Round 2, Phase 7.1: typed in-process events.</summary>
public class EventBusTests
{
    private sealed class CollectingSink : IMessageSink
    {
        public List<(string, int)> Posted { get; } = new();
        public void Post(string message, int durationSeconds = 3) => Posted.Add((message, durationSeconds));
    }

    private static Universe Started()
    {
        var u = new Universe("Bus", "bus-seed") { NebulaFactory = Nebula.CreateWithoutPixels };
        u.Generate(new RecordingAssetRequests());
        return u;
    }

    [Fact]
    public void Publish_ReachesOnlyMatchingSubscribers_UntilDisposed()
    {
        var bus = new EventBus();
        var strings = new List<string>();
        var ints = new List<int>();
        var sub = bus.Subscribe<string>(strings.Add);
        bus.Subscribe<int>(ints.Add);

        bus.Publish("a");
        sub.Dispose();
        bus.Publish("b");
        bus.Publish(7);

        Assert.Equal(new[] { "a" }, strings);
        Assert.Equal(new[] { 7 }, ints);
    }

    [Fact]
    public void Handler_MayUnsubscribeWhileHandling()
    {
        var bus = new EventBus();
        int calls = 0;
        System.IDisposable sub = null;
        sub = bus.Subscribe<int>(_ => { calls++; sub.Dispose(); });

        bus.Publish(1);
        bus.Publish(1);

        Assert.Equal(1, calls);
    }

    [Fact]
    public void EnterSystem_PublishesSystemEntered()
    {
        var u = Started();
        var entered = new List<SystemEntered>();
        u.Events.Subscribe<SystemEntered>(entered.Add);

        var next = u.Galaxy.FindById(u.ActiveStarSystem.SystemConnectionIds.First());
        var system = u.EnterSystem(next);

        Assert.Single(entered);
        Assert.Same(system, entered[0].System);
        u.Dispose();
    }

    [Fact]
    public void Messages_FlowThroughBusToSink()
    {
        var u = new Universe("Msg", "msg-seed") { NebulaFactory = Nebula.CreateWithoutPixels };
        var sink = new CollectingSink();
        u.Generate(new RecordingAssetRequests(), sink);
        var seen = new List<MessageRequested>();
        u.Events.Subscribe<MessageRequested>(seen.Add);

        u.Messages.Post("hello", 5);

        Assert.Equal(new[] { ("hello", 5) }, sink.Posted);
        Assert.Single(seen);
        u.Dispose();
    }

    [Fact]
    public void Damage_PublishesShipDamaged_AndLegacyShipHit_AndShipDestroyedOnce()
    {
        var u = Started();
        var npc = new Nonplayer("npc-1");
        npc.CurrentHullStrength = 50;
        npc.CurrentShieldStrength = 0;
        u.ActiveStarSystem.AddNpc(npc);
        var damaged = new List<ShipDamaged>();
        var destroyed = new List<ShipDestroyed>();
        int legacy = 0;
        u.Events.Subscribe<ShipDamaged>(damaged.Add);
        u.Events.Subscribe<ShipDestroyed>(destroyed.Add);
        u.ActiveStarSystem.ShipHit += (_, _, _) => legacy++;

        var shot = new Projectile { Owner = u.Player, Damage = 5, Lifetime = 1f };
        npc.ApplyDamage(shot);
        Assert.Single(damaged);
        Assert.Equal(1, legacy);
        Assert.Empty(destroyed);

        var kill = new Projectile { Owner = u.Player, Damage = 100000, Lifetime = 1f };
        npc.ApplyDamage(kill);
        npc.ApplyDamage(kill); // already destroyed: ignored

        Assert.Single(destroyed);
        Assert.Same(npc, destroyed[0].Ship);
        Assert.Same(u.Player, destroyed[0].Killer);
        Assert.Equal(2, damaged.Count);
        u.Dispose();
    }
}
