using System.Linq;
using Microsoft.Xna.Framework;
using Strange_Universe.Game.Entities;
using Xunit;

namespace Strange_Universe.Tests;

/// <summary>Round 2, Phase 4: generation is pure and galaxy expansion happens on entry.</summary>
public class PureGenerationTests
{
    private static (Universe universe, StarSystemNode node) Build(string seed = "pure-seed")
    {
        var universe = new Universe("Pure", seed) { NebulaFactory = Nebula.CreateWithoutPixels };
        var node = new StarSystemNode(seed, Vector2.Zero, existingNodes: universe.StarSystemNodes);
        universe.StarSystemNodes.Add(node);
        universe.Player.CurrentStarSystemID = node.SystemId;
        return (universe, node);
    }

    private static string Describe(StarSystemLayout l) => string.Join("|",
        l.PlanetCount, l.AsteroidCount, l.StarCount, l.SystemRadius, l.MandevilleRadius, l.StarOrbitRadius,
        l.AsteroidBeltInnerRadius, l.AsteroidBeltOuterRadius, l.BackgroundStarCount, l.SystemConnectionCount,
        l.InnerPlanetCount, l.NebulaIndex,
        string.Join(";", l.Stars.Select(s => s.Name + s.Position)),
        string.Join(";", l.Planets.Select(p => p.Id + p.Position)),
        string.Join(";", l.Asteroids.Select(a => a.Position)),
        string.Join(";", l.BackgroundStars.Select(b => b.Position)));

    [Fact]
    public void Generate_SameNode_GivesEqualLayouts()
    {
        var (_, node) = Build();
        Assert.Equal(Describe(StarSystemGenerator.Generate(node, Universe.NebulaPoolSize)),
                     Describe(StarSystemGenerator.Generate(node, Universe.NebulaPoolSize)));
    }

    [Fact]
    public void Generate_DoesNotMutateNodeOrUniverse()
    {
        var (universe, node) = Build();
        string Snapshot() => string.Join("|", universe.StarSystemNodes.Select(n =>
            $"{n.SystemId}:{n.Discovered}:[{string.Join(",", n.SystemConnectionIds.OrderBy(x => x))}]"));
        string before = Snapshot();

        StarSystemGenerator.Generate(node, Universe.NebulaPoolSize);

        Assert.Equal(before, Snapshot());
        Assert.False(node.Discovered);
    }

    [Fact]
    public void Generate_Home_UsesSolOverrides()
    {
        var (_, node) = Build();
        Assert.True(node.IsHome);
        var layout = StarSystemGenerator.Generate(node, Universe.NebulaPoolSize);
        Assert.Equal(8, layout.PlanetCount);
        Assert.Equal(4, layout.SystemConnectionCount);
    }

    [Fact]
    public void EnterSystem_ExpandsGalaxyOnlyOnFirstEntry()
    {
        var (universe, node) = Build();

        universe.EnterSystem(node);
        Assert.True(node.Discovered);
        Assert.True(node.SystemConnectionIds.Count >= 1);

        // Simulate a connection that the generator would otherwise add back.
        string removed = node.SystemConnectionIds.First();
        node.SystemConnectionIds.Remove(removed);

        universe.EnterSystem(node);

        Assert.DoesNotContain(removed, node.SystemConnectionIds);
        universe.Dispose();
    }

    [Fact]
    public void IsHome_IsNotSerialized()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new StarSystemNode { Name = "Sol" });
        Assert.DoesNotContain("IsHome", json);
    }
}
