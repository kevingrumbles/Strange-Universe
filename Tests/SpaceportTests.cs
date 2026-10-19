using System.Collections.Generic;
using System.Linq;
using Strange_Universe.Game.Components.PlanetComponents;
using Strange_Universe.Game.Entities;
using Xunit;

namespace Strange_Universe.Tests;

/// <summary>Which planets have a spaceport, and what its services do.</summary>
public class SpaceportTests
{
    private static Planet PlanetNumber(int i, string system = "sys") =>
        new($"{system}_Planet_{i}", 90f, 200f, 3000f, 9000f, 1 + i % 8, 8);

    private static List<Planet> ManyPlanets(string system = "sys") =>
        Enumerable.Range(0, 600).Select(i => PlanetNumber(i, system)).ToList();

    [Fact]
    public void GasGiants_NeverHaveASpaceport()
    {
        var gasGiants = ManyPlanets().Where(p => p.Type == PlanetType.GasGiant).ToList();

        Assert.NotEmpty(gasGiants); // the sample really does include some
        Assert.All(gasGiants, p => Assert.Null(p.Spaceport));
    }

    [Fact]
    public void EveryOtherType_HasAChance_SoSomeHaveOneAndSomeDoNot()
    {
        var planets = ManyPlanets();

        foreach (var type in System.Enum.GetValues<PlanetType>().Where(t => t != PlanetType.GasGiant))
        {
            var ofType = planets.Where(p => p.Type == type).ToList();
            Assert.NotEmpty(ofType);
            Assert.Contains(ofType, p => p.Spaceport != null);
            Assert.Contains(ofType, p => p.Spaceport == null);
        }
    }

    [Fact]
    public void ThePlanetsThatHaveASpaceport_AreTheSameEveryTimeAPlanetIsMade()
    {
        var first = ManyPlanets("seed-a").Select(p => p.Spaceport != null).ToList();
        var second = ManyPlanets("seed-a").Select(p => p.Spaceport != null).ToList();

        Assert.Equal(first, second);
        Assert.NotEqual(first, ManyPlanets("seed-b").Select(p => p.Spaceport != null).ToList()); // and it varies by planet id
    }

    [Fact]
    public void ASpaceportOffersRefuelAndRepair_AndDoesNotDependOnAnyShip()
    {
        var spaceport = ManyPlanets().First(p => p.Spaceport != null).Spaceport;

        Assert.False(string.IsNullOrWhiteSpace(spaceport.Description));
        Assert.Contains(spaceport.Services, s => s is RefuelService);
        Assert.Contains(spaceport.Services, s => s is RepairService);
        Assert.All(spaceport.Services, s => Assert.False(string.IsNullOrWhiteSpace(s.Name)));
    }

    [Fact]
    public void EachSpaceport_HasItsOwnServiceObjects()
    {
        var withPorts = ManyPlanets().Where(p => p.Spaceport != null).Take(2).ToList();

        Assert.NotSame(withPorts[0].Spaceport, withPorts[1].Spaceport);
        Assert.NotSame(withPorts[0].Spaceport.Services[0], withPorts[1].Spaceport.Services[0]);
    }

    [Fact]
    public void Refuel_FillsTheFuel_AndRepair_FillsTheHull()
    {
        var universe = new Universe("Port", "port-seed");
        var player = universe.Player;
        player.CurrentFuelLevel = 0;
        player.CurrentHullStrength = 1;

        new RefuelService().Perform(universe);
        Assert.Equal(player.MaxFuelLevel, player.CurrentFuelLevel);
        Assert.Equal(1, player.CurrentHullStrength); // refuelling does not repair

        new RepairService().Perform(universe);
        Assert.Equal(player.MaxHullStrength, player.CurrentHullStrength);
    }

    [Fact]
    public void Repair_LeavesTheShieldAlone()
    {
        var universe = new Universe("Port", "port-seed");
        universe.Player.CurrentShieldStrength = 0;
        universe.Player.CurrentHullStrength = 1;

        new RepairService().Perform(universe);

        Assert.Equal(0, universe.Player.CurrentShieldStrength); // shields come back by entering a system, not here
    }
}
