using System;
using System.Linq;
using Strange_Universe.Game.Components.ShipComponents;
using Strange_Universe.Game.Entities.ShipParts;
using Strange_Universe.Game.Systems;
using Xunit;

namespace Strange_Universe.Tests;

/// <summary>Ships are drawn from a generated picture, with flames at the engines the generator placed.</summary>
public class ShipHullTests
{
    private static readonly ShipStats[] Types = { new Shuttle(), new Runner() };

    private static int Alpha(ShipHull hull, int x, int y) => (int)(hull.Pixels[y * ShipHull.AtlasWidth + x] >> 24); // the first picture

    [Fact]
    public void TheSameAppearance_MakesTheSameShip_AndADifferentSeedDoesNot()
    {
        var a = new Runner().Appearance;
        var b = new Runner().Appearance;
        var other = new Runner().Appearance;
        other.HullSeed += 1;

        Assert.Equal(ShipHullGenerator.Generate(a).Pixels, ShipHullGenerator.Generate(b).Pixels);
        Assert.NotEqual(ShipHullGenerator.Generate(a).Pixels, ShipHullGenerator.Generate(other).Pixels);
    }

    [Fact]
    public void EachShipType_LooksDifferent_AndFillsMuchOfItsPicture()
    {
        var pictures = Types.Select(t => ShipHullGenerator.Generate(t.Appearance).Pixels).ToList();

        Assert.NotEqual(pictures[0], pictures[1]);
        foreach (var pixels in pictures)
        {
            int solid = pixels.Count(p => (p >> 24) == 255);
            Assert.InRange(solid, ShipHull.Size * ShipHull.Size * ShipHull.LightAngles / 10, ShipHull.Size * ShipHull.Size * ShipHull.LightAngles / 2);
        }
    }

    [Fact]
    public void TheShipIsMirroredAboutItsAxis()
    {
        foreach (var type in Types)
        {
            var hull = ShipHullGenerator.Generate(type.Appearance);
            for (int y = 0; y < ShipHull.Size / 2; y++)
                for (int x = 0; x < ShipHull.Size; x++)
                    Assert.Equal(Alpha(hull, x, y), Alpha(hull, x, ShipHull.Size - 1 - y));
        }
    }

    [Fact]
    public void EveryEngine_HasAFlameStartingAtTheBackOfTheShip_OnTheHull()
    {
        foreach (var type in Types)
        {
            var hull = ShipHullGenerator.Generate(type.Appearance);

            Assert.Equal(type.Appearance.EngineCount, hull.Engines.Length);
            foreach (var engine in hull.Engines)
            {
                Assert.True(engine.X < -0.9f, "engines are at the tail");
                int px = (int)((engine.X + 1f) * 0.5f * ShipHull.Size) + 4;
                int py = (int)((engine.Y + 1f) * 0.5f * ShipHull.Size);
                Assert.Equal(255, Alpha(hull, px, py));
            }
        }
    }

    [Fact]
    public void TheFlame_IsBrightestAtTheNozzle_AndFadesToNothingAtTheTip()
    {
        var flame = ShipHullGenerator.Flame();
        int centre = ShipHullGenerator.FlameWidth / 2;

        uint Alpha(int y) => flame[y * ShipHullGenerator.FlameWidth + centre] >> 24;

        Assert.True(Alpha(2) > Alpha(30));
        Assert.True(Alpha(30) > Alpha(ShipHullGenerator.FlameHeight - 2));
        Assert.Equal(0u, Alpha(ShipHullGenerator.FlameHeight - 1));
    }

    [Fact]
    public void ThrustLevel_SwellsWhileThrusting_AndFadesWhenTheEnginesStop()
    {
        var ship = new Nonplayer("flame");
        Assert.Equal(0f, ship.ThrustLevel);

        for (int i = 0; i < 3; i++)
        {
            ship.ApplyThrust(1f / 60f);
            ship.Update(1f / 60f);
        }
        float firing = ship.ThrustLevel;
        Assert.InRange(firing, 0.2f, 1f);

        ship.Update(1f / 60f); // no thrust this frame
        Assert.True(ship.ThrustLevel < firing);

        for (int i = 0; i < 120; i++) ship.Update(1f / 60f);
        Assert.Equal(0f, ship.ThrustLevel);
    }
}
