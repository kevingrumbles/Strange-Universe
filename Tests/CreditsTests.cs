using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Strange_Universe.Game.Components.ShipComponents;
using Strange_Universe.Game.Entities;
using Strange_Universe.Game.Entities.ShipParts;
using Xunit;

namespace Strange_Universe.Tests;

/// <summary>Ships carry credits: the player spends them, NPCs have some to be looted later.</summary>
public class CreditsTests
{
    // -- spending and earning ---------------------------------------------------------

    [Fact]
    public void CanAfford_IsTrueOnlyForAWholeAmountTheShipHas()
    {
        var ship = new Nonplayer("buyer") { Credits = 100 };

        Assert.True(ship.CanAfford(0));
        Assert.True(ship.CanAfford(100));
        Assert.False(ship.CanAfford(101));
        Assert.False(ship.CanAfford(-1));
    }

    [Fact]
    public void Earning_AddsCredits()
    {
        var ship = new Nonplayer("seller") { Credits = 5 };

        ship.Earn(95);

        Assert.Equal(100, ship.Credits);
    }

    [Fact]
    public void ANegativeAmount_IsRejected_WhenEarning()
    {
        var ship = new Nonplayer("odd") { Credits = 50 };

        Assert.Throws<ArgumentOutOfRangeException>(() => ship.Earn(-1));
        Assert.Equal(50, ship.Credits);
    }

    // -- the player -------------------------------------------------------------------------

    [Fact]
    public void ANewPlayer_StartsWithTheStartingCredits()
    {
        var universe = new Universe("Rich", "rich-seed");

        Assert.Equal(Universe.StartingCredits, universe.Player.Credits);
        Assert.True(universe.Player.Credits > 0);
    }

    [Fact]
    public void ThePlayersCredits_AreSavedAndLoaded()
    {
        string path = Path.Combine(Path.GetTempPath(), $"su-credits-{Guid.NewGuid():N}.json");
        try
        {
            var universe = new Universe("Saver", "saver-seed");
            universe.Player.Credits = 4321;
            new FileSaveService(path).Save(universe);

            var loaded = new FileSaveService(path).LoadAll().Single();

            Assert.Equal(4321, loaded.Player.Credits);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void ASaveFromBeforeCredits_LoadsWithNone()
    {
        string sample = Path.Combine(AppContext.BaseDirectory, "Data", "sample-universe-settings.json");
        string json = File.ReadAllText(sample);
        Assert.Contains("\"Credits\": 120", json);

        // The same save with the credits taken out is what a file from before credits existed looks like.
        string old = System.Text.RegularExpressions.Regex.Replace(json, @"\s*""Credits"": \d+,", "");
        Assert.DoesNotContain("Credits", old);

        var withCredits = JsonSerializer.Deserialize<System.Collections.Generic.List<Universe>>(json, FileSaveService.ReadOptions);
        var without = JsonSerializer.Deserialize<System.Collections.Generic.List<Universe>>(old, FileSaveService.ReadOptions);

        Assert.All(withCredits, u => Assert.Equal(120, u.Player.Credits));
        Assert.All(without, u => Assert.Equal(0, u.Player.Credits));
    }

    // -- NPCs ---------------------------------------------------------------------------------

    [Fact]
    public void AnNpcHasCredits_InARangeAroundItsShipTypesTypicalAmount()
    {
        foreach (ShipStats type in new ShipStats[] { new Shuttle(), new Runner() })
        {
            Assert.True(type.TypicalCredits > 0, type.ShipTypeName);

            for (int i = 0; i < 200; i++)
            {
                var npc = new Nonplayer($"npc-{type.ShipTypeName}-{i}", type.ShipTypeName == "Runner" ? new Runner() : new Shuttle());

                Assert.InRange(npc.Credits, (int)(type.TypicalCredits * 0.5) - 1, (int)(type.TypicalCredits * 1.5) + 1);
            }
        }
    }

    [Fact]
    public void AnNpcsCredits_AreTheSameEveryTimeItIsMade_AndDifferBetweenNpcs()
    {
        int first = new Nonplayer("npc-fixed", new Runner()).Credits;
        int again = new Nonplayer("npc-fixed", new Runner()).Credits;
        var amounts = Enumerable.Range(0, 100).Select(i => new Nonplayer($"npc-{i}", new Runner()).Credits).ToList();

        Assert.Equal(first, again);
        Assert.True(amounts.Distinct().Count() > 20, "NPCs should not all carry the same amount");
    }

    [Fact]
    public void ARunnerNpc_CarriesMoreThanAShuttleNpc_OnAverage()
    {
        double runners = Enumerable.Range(0, 200).Average(i => new Nonplayer($"r-{i}", new Runner()).Credits);
        double shuttles = Enumerable.Range(0, 200).Average(i => new Nonplayer($"s-{i}", new Shuttle()).Credits);

        Assert.True(runners > shuttles);
    }
}
