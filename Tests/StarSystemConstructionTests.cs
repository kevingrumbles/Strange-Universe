using System.Linq;
using Microsoft.Xna.Framework;
using Strange_Universe.Game.Entities;
using Xunit;

namespace Strange_Universe.Tests;

/// <summary>
/// StarSystem can now be built without a GraphicsDevice or any Launcher state.
/// </summary>
public class StarSystemConstructionTests
{
    private static (Universe universe, StarSystemNode node) BuildUniverse(string seed)
    {
        var universe = new Universe("Test", seed);
        var node = new StarSystemNode(seed, Vector2.Zero, existingNodes: universe.StarSystemNodes);
        universe.StarSystemNodes.Add(node);
        universe.Player.CurrentStarSystemID = node.SystemId;
        return (universe, node);
    }

    [Fact]
    public void FirstNode_IsSol()
    {
        var (_, node) = BuildUniverse("seed-1");
        Assert.Equal("Sol", node.Name);
        Assert.Equal("seed-1_Sol", node.SystemId);
    }

    [Fact]
    public void Construct_WithoutGraphics_Succeeds()
    {
        var (universe, node) = BuildUniverse("seed-1");

        var system = new StarSystem(node, universe, assets: null);

        Assert.Same(universe, system.Universe);
        Assert.Same(universe.Player, system.ActivePlayer);
        Assert.Equal(8, system.Planets.Count);   // Sol overrides
        Assert.Single(system.Stars);
        Assert.Equal(100, system.Asteroids.Count);
        Assert.True(node.Discovered);
        Assert.True(universe.StarSystemNodes.Count > 1); // connections generated
    }

    [Fact]
    public void Construct_IsDeterministic()
    {
        var (u1, n1) = BuildUniverse("determinism");
        var (u2, n2) = BuildUniverse("determinism");

        var a = new StarSystem(n1, u1, null);
        var b = new StarSystem(n2, u2, null);

        Assert.Equal(a.SystemRadius, b.SystemRadius);
        Assert.Equal(a.Planets.Select(p => (p.Name, p.Position)), b.Planets.Select(p => (p.Name, p.Position)));
        Assert.Equal(a.Asteroids.Select(x => x.Position), b.Asteroids.Select(x => x.Position));
        Assert.Equal(u1.StarSystemNodes.Select(n => n.SystemId), u2.StarSystemNodes.Select(n => n.SystemId));
    }

    [Fact]
    public void ActiveStarSystem_AttachesPlayer()
    {
        var universe = new Universe("Test", "attach-seed");
        Assert.Null(universe.Player.StarSystem);

        var system = universe.ActiveStarSystem;

        Assert.Same(system, universe.Player.StarSystem);
    }

    [Fact]
    public void AddNpc_AttachesSystem()
    {
        var (universe, node) = BuildUniverse("npc-seed");
        var system = new StarSystem(node, universe, null);
        var npc = new Nonplayer("npc-1");

        Assert.Null(npc.StarSystem);
        system.AddNpc(npc);

        Assert.Same(system, npc.StarSystem);
        Assert.Contains(npc, system.Npcs);
    }
}
