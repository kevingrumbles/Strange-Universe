using System;
using Microsoft.Xna.Framework;
using Strange_Universe.Game.Entities;
using Xunit;

namespace Strange_Universe.Tests;

public class SystemSpatialQueriesTests
{
    private static StarSystem Build(string seed, int randomSeed)
    {
        var universe = new Universe("Spatial", seed);
        var node = new StarSystemNode(seed, Vector2.Zero, existingNodes: universe.StarSystemNodes);
        universe.StarSystemNodes.Add(node);
        universe.Player.CurrentStarSystemID = node.SystemId;
        return new StarSystem(node, universe, null, new Random(randomSeed));
    }

    [Fact]
    public void SeededRandom_MakesQueriesDeterministic()
    {
        var a = Build("spatial", 42);
        var b = Build("spatial", 42);

        Assert.Equal(a.Spatial.GetRandomSafeLocationOutsideAsteroidBelt(), b.Spatial.GetRandomSafeLocationOutsideAsteroidBelt());
        Assert.Equal(a.Spatial.GetSystemEdgeEntryPosition(), b.Spatial.GetSystemEdgeEntryPosition());
        Assert.Equal(a.Spatial.GetSafeEntryTransform().Position, b.Spatial.GetSafeEntryTransform().Position);
    }

    [Fact]
    public void GetSafeLocation_PushesClearOfStar()
    {
        var s = Build("spatial", 1);
        Vector2 safe = s.Spatial.GetSafeLocation(Vector2.Zero);
        Assert.True(safe.Length() >= s.Stars[0].Radius + 1000f - 0.01f);
    }

    [Fact]
    public void Galaxy_FindsNodesByIdAndPosition()
    {
        var s = Build("graph", 1);
        var galaxy = s.Universe.Galaxy;

        foreach (var node in s.Universe.StarSystemNodes)
        {
            Assert.Same(node, galaxy.FindById(node.SystemId));
            Assert.Same(node, galaxy.FindByPosition(node.GalaxyPosition));
        }
        Assert.Null(galaxy.FindById("missing"));
    }
}
