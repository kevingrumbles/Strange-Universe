using System;
using System.IO;
using System.Linq;
using Strange_Universe.Game.Components;
using Strange_Universe.Game.Entities;
using Strange_Universe.Game.Entities.ShipParts;
using Strange_Universe.Tests.Fakes;
using Xunit;

namespace Strange_Universe.Tests;

/// <summary>A destroyed player ends the session without saving; the menu then offers the last saved state.</summary>
public class PlayerDeathTests
{
    private static Universe Started(string seed = "death-seed")
    {
        var u = new Universe("Death", seed);
        u.Generate(null);
        u.Player.Equipment.Clear(); // no shield: an active shield would absorb even a huge hit, so one big shot is lethal
        u.Player.CurrentShieldStrength = 0;
        return u;
    }

    private static Projectile Lethal(Ship owner) => new() { Owner = owner, Damage = 100000, Lifetime = 1f };

    // -- knowing the player is destroyed ------------------------------------

    [Fact]
    public void ALethalHit_DestroysThePlayer_AndPublishesItOnce()
    {
        var u = Started();
        var npc = new Nonplayer("killer");
        u.ActiveStarSystem.AddNpc(npc);
        var destroyed = new System.Collections.Generic.List<ShipDestroyed>();
        u.Events.Subscribe<ShipDestroyed>(destroyed.Add);
        Assert.False(u.Player.IsDestroyed);

        u.Player.ApplyDamage(Lethal(npc));
        u.Player.ApplyDamage(Lethal(npc)); // a ship that is already destroyed is not destroyed again

        Assert.True(u.Player.IsDestroyed);
        Assert.Equal(u.Player, Assert.Single(destroyed).Ship);
        u.Dispose();
    }

    [Fact]
    public void HitsThatDoNotKill_AndOtherShipsDying_LeaveThePlayerAlive()
    {
        var u = Started();
        var npc = new Nonplayer("victim");
        u.ActiveStarSystem.AddNpc(npc);
        u.Player.CurrentHullStrength = 1000;
        u.Player.CurrentShieldStrength = 0;

        u.Player.ApplyDamage(new Projectile { Owner = npc, Damage = 10, Lifetime = 1f });
        npc.CurrentShieldStrength = 0; // so the one big shot is not absorbed
        npc.ApplyDamage(Lethal(u.Player)); // an NPC is destroyed

        Assert.True(npc.IsDestroyed);
        Assert.False(u.Player.IsDestroyed);
        u.Dispose();
    }

    [Fact]
    public void AnNpcShotKillingThePlayer_InAFullSystemUpdate_IsDetected()
    {
        var u = Started();
        var system = u.ActiveStarSystem;
        u.Player.Navigator.Clear();
        u.Player.Position = new Microsoft.Xna.Framework.Vector2(0, 5000);
        u.Player.Velocity = Microsoft.Xna.Framework.Vector2.Zero;

        // A shot that is about to hit the player (the collision system sweeps from where it was).
        system.Projectiles.Add(new Projectile
        {
            Owner = new Nonplayer("shooter"),
            Position = u.Player.Position,
            Velocity = new Microsoft.Xna.Framework.Vector2(0, -100),
            Damage = 100000,
            Lifetime = 5f,
            Radius = 10f,
        });
        system.Update(1f / 60f, new InputState());

        Assert.True(u.Player.IsDestroyed);
        u.Dispose();
    }

    // -- discarding the session ---------------------------------------------

    [Fact]
    public void ReloadInto_ReplacesInMemoryChanges_WithTheSavedState()
    {
        string path = Path.Combine(Path.GetTempPath(), $"su-death-{Guid.NewGuid():N}.json");
        try
        {
            var saves = new FileSaveService(path);
            var universe = new Universe("Saved", "saved-seed");
            universe.Player.CurrentHullStrength = 1;
            saves.Save(universe);
            var list = saves.LoadAll();
            var live = list.Single();

            // The session goes badly: the player is destroyed, and the universe is never saved again.
            live.Player.CurrentHullStrength = 0;
            Assert.True(live.Player.IsDestroyed);

            saves.ReloadInto(list);

            var reloaded = list.Single();
            Assert.NotSame(live, reloaded);
            Assert.Equal(universe.Id, reloaded.Id);
            Assert.False(reloaded.Player.IsDestroyed);
            Assert.Equal(1, reloaded.Player.CurrentHullStrength);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void ReloadInto_DropsUniversesThatWereNeverSaved_AndKeepsTheSavedOnes()
    {
        string path = Path.Combine(Path.GetTempPath(), $"su-death-{Guid.NewGuid():N}.json");
        try
        {
            var saves = new FileSaveService(path);
            saves.Save(new Universe("Old", "old-seed"));
            var list = saves.LoadAll();
            list.Add(new Universe("Brand new", "new-seed")); // created this session, never saved

            saves.ReloadInto(list);

            Assert.Equal(new[] { "Old" }, list.Select(u => u.Name));
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void ReloadInto_WorksWithTheInMemoryService()
    {
        var saves = new InMemorySaveService();
        saves.Save(new Universe("One", "one-seed"));
        var list = new System.Collections.Generic.List<Universe> { new("Unsaved", "x") };

        saves.ReloadInto(list);

        Assert.Equal(new[] { "One" }, list.Select(u => u.Name));
    }
}
