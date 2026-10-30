using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Strange_Universe.Game.Components;
using Strange_Universe.Game.Components.EquipmentComponents;
using Strange_Universe.Game.Components.ShipComponents;
using Strange_Universe.Game.Entities;
using Strange_Universe.Game.Entities.ShipParts;
using Strange_Universe.Tests.Fakes;
using Xunit;

namespace Strange_Universe.Tests;

/// <summary>Buying and selling at a spaceport: what is charged, what is refused, and what the player ends up with.</summary>
public class TradeTests
{
    private sealed class Docked
    {
        public Universe Universe { get; }
        public List<string> Messages { get; } = new();
        public Player Player => Universe.Player;

        public Docked(bool docked = true)
        {
            Universe = new Universe("Trader", "trade-seed");
            Universe.Generate(null);
            Universe.Events.Subscribe<MessageRequested>(m => Messages.Add(m.Message));
            Player.Navigator.Clear();

            var planet = DockedWorld.PlanetWith(spaceport: true);
            Universe.ActiveStarSystem.Planets.Clear();
            Universe.ActiveStarSystem.Planets.Add(planet);
            Player.Position = planet.Position + new Vector2(planet.Radius + 20f, 0f);
            if (docked) Universe.Dock(planet);
            Messages.Clear();
        }
    }

    /// <summary>A new gun, as a stocked item is: a fresh object nobody holds.</summary>
    private static WeaponComponent Gun(int price = 200, float mass = 10f)
    {
        var gun = TestGear.Gun();
        gun.Price = price;
        gun.Mass = mass;
        return gun;
    }

    /// <summary>Gives the player exactly this gear and these limits and credits, so the numbers do not depend on any ship type.</summary>
    private static void Prepare(Docked d, int credits, int weapons = 2, float maxMass = 1000f, params EquipmentComponent[] gear)
    {
        TestGear.Arm(d.Player, gear);
        d.Player.ShipType.Limits.MaxWeapons = weapons;
        d.Player.ShipType.Limits.MaxMass = maxMass;
        d.Player.Credits = credits;
    }

    // -- buying equipment -------------------------------------------------------------------

    [Fact]
    public void BuyingEquipment_SpendsThePrice_AndInstallsTheItem()
    {
        var d = new Docked();
        Prepare(d, credits: 500);
        var gun = Gun(price: 200);

        Assert.True(d.Universe.BuyEquipment(gun));

        Assert.Equal(300, d.Player.Credits);
        Assert.Same(gun, Assert.Single(d.Player.Equipment));
        Assert.Contains(d.Messages, m => m.Contains("Bought"));
        d.Universe.Dispose();
    }

    [Fact]
    public void ItemsFromTheCatalog_AreFreshEveryTime_SoBuyingTheSameOneTwiceGivesTwoSeparateItems()
    {
        var d = new Docked();
        Prepare(d, credits: 100000, weapons: 9, maxMass: 100000f);
        var firstRead = EquipmentCatalog.All.First(item => item is WeaponComponent);
        var secondRead = EquipmentCatalog.All.First(item => item is WeaponComponent);

        Assert.True(d.Universe.BuyEquipment(firstRead));
        Assert.True(d.Universe.BuyEquipment(secondRead));

        Assert.Equal(2, d.Player.Equipment.Count);
        Assert.NotSame(d.Player.Equipment[0], d.Player.Equipment[1]);
        d.Universe.Dispose();
    }

    [Fact]
    public void WithJustEnoughCredits_TheBuyGoesThrough_LeavingNone()
    {
        var d = new Docked();
        Prepare(d, credits: 200);

        Assert.True(d.Universe.BuyEquipment(Gun(price: 200)));

        Assert.Equal(0, d.Player.Credits);
        d.Universe.Dispose();
    }

    [Fact]
    public void TooFewCredits_RefusesAndChangesNothing()
    {
        var d = new Docked();
        Prepare(d, credits: 199);

        Assert.False(d.Universe.BuyEquipment(Gun(price: 200)));

        Assert.Equal(199, d.Player.Credits);
        Assert.Empty(d.Player.Equipment);
        Assert.Contains(d.Messages, m => m.Contains("Not enough credits"));
        d.Universe.Dispose();
    }

    [Fact]
    public void NoFreeSlot_RefusesAndChangesNothing()
    {
        var d = new Docked();
        Prepare(d, credits: 1000, weapons: 1, gear: Gun());

        Assert.False(d.Universe.BuyEquipment(Gun()));

        Assert.Equal(1000, d.Player.Credits);
        Assert.Single(d.Player.Equipment);
        Assert.Contains(d.Messages, m => m.Contains("No free slot"));
        d.Universe.Dispose();
    }

    [Fact]
    public void TooHeavy_RefusesAndChangesNothing_ButExactlyTheLimitIsFine()
    {
        var d = new Docked();
        Prepare(d, credits: 1000, weapons: 3, maxMass: 100f, gear: Gun(mass: 60f));

        Assert.False(d.Universe.BuyEquipment(Gun(mass: 41f)));
        Assert.Equal(1000, d.Player.Credits);
        Assert.Single(d.Player.Equipment);
        Assert.Contains(d.Messages, m => m.Contains("too heavy"));

        Assert.True(d.Universe.BuyEquipment(Gun(mass: 40f))); // 100 of 100
        d.Universe.Dispose();
    }

    [Fact]
    public void CreditsAreCheckedBeforeWhetherItFits()
    {
        var d = new Docked();
        Prepare(d, credits: 10, weapons: 0); // cannot afford it and has no slot for it

        Assert.False(d.Universe.BuyEquipment(Gun(price: 200)));

        Assert.Equal("Not enough credits", Assert.Single(d.Messages));
        d.Universe.Dispose();
    }

    [Fact]
    public void WhenNotDocked_NothingCanBeBoughtOrSold()
    {
        var d = new Docked(docked: false);
        Prepare(d, credits: 5000, weapons: 2);
        var owned = Gun();
        d.Player.Equipment.Add(owned);

        Assert.False(d.Universe.BuyEquipment(Gun()));
        Assert.False(d.Universe.BuyShip(new Runner()));
        Assert.False(d.Universe.SellEquipment(owned));

        Assert.Equal(5000, d.Player.Credits);
        Assert.Same(owned, Assert.Single(d.Player.Equipment));
        d.Universe.Dispose();
    }

    // -- selling equipment -------------------------------------------------------------------

    [Fact]
    public void SellingEquipment_RemovesIt_AndPaysItsPrice()
    {
        var d = new Docked();
        var gun = Gun(price: 350);
        Prepare(d, credits: 100, gear: gun);

        Assert.True(d.Universe.SellEquipment(gun));

        Assert.Equal(450, d.Player.Credits);
        Assert.Empty(d.Player.Equipment);
        Assert.Contains(d.Messages, m => m.Contains("Sold"));
        d.Universe.Dispose();
    }

    [Fact]
    public void SellingSomethingTheShipDoesNotCarry_ChangesNothing()
    {
        var d = new Docked();
        Prepare(d, credits: 100);

        Assert.False(d.Universe.SellEquipment(Gun()));

        Assert.Equal(100, d.Player.Credits);
        d.Universe.Dispose();
    }

    [Fact]
    public void SellingThenBuyingBack_CostsNothingInTheEnd_AndFreesTheSlotMeanwhile()
    {
        var d = new Docked();
        var gun = Gun(price: 300);
        Prepare(d, credits: 350, weapons: 1, gear: gun);
        Assert.False(d.Universe.BuyEquipment(Gun(price: 300))); // full

        d.Universe.SellEquipment(gun);
        Assert.True(d.Universe.BuyEquipment(Gun(price: 300)));

        Assert.Equal(350, d.Player.Credits);
        d.Universe.Dispose();
    }

    // -- buying a ship -------------------------------------------------------------------------

    [Fact]
    public void ABoughtShip_CostsItsListPrice_LessWhatTheCurrentShipAndItsEquipmentAreWorth()
    {
        var d = new Docked();
        d.Player.AssignShipType(new Shuttle());
        d.Player.Equipment.Add(new UtilityComponent { EquipmentName = "Extra", Price = 90 }); // the equipment counts in the trade-in too
        var type = new Runner();
        int worth = d.Player.ShipType.Price + d.Player.EquipmentValue;
        int cost = type.ListPrice - worth;
        Assert.True(cost > 0);
        d.Player.Credits = cost + 7;
        Assert.Equal(cost, d.Universe.ShipCostAfterTradeIn(new Runner())); // the price shown is the price charged

        Assert.True(d.Universe.BuyShip(type));

        Assert.Equal(7, d.Player.Credits);
        d.Universe.Dispose();
    }

    [Fact]
    public void ABoughtShip_ReplacesTheOldOne_WithItsDefaultLoadoutAndFullSystems()
    {
        var d = new Docked();
        d.Player.AssignShipType(new Shuttle());
        d.Player.CurrentHullStrength = 1;
        d.Player.Equipment.Add(new UtilityComponent { EquipmentName = "Old Extra" });
        string name = d.Player.Name;
        Vector2 where = d.Player.Position;
        d.Player.Credits = 100000;

        d.Universe.BuyShip(new Runner());

        Assert.Equal("Runner", d.Player.ShipType.ShipTypeName);
        Assert.Equal(d.Player.ShipType.DefaultLoadout, d.Player.Equipment);           // the new ship's own default loadout, nothing of the old
        Assert.DoesNotContain(d.Player.Equipment, item => item.EquipmentName == "Old Extra");
        Assert.Equal(d.Player.MaxHullStrength, d.Player.CurrentHullStrength);         // full again
        Assert.Equal(d.Player.MaxShieldStrength, d.Player.CurrentShieldStrength);
        Assert.Equal(d.Player.MaxFuelLevel, d.Player.CurrentFuelLevel);
        Assert.Equal(name, d.Player.Name);
        Assert.Equal(where, d.Player.Position);
        Assert.True(d.Universe.IsDocked);                                             // and the player is still docked
        d.Universe.Dispose();
    }

    [Fact]
    public void ABoughtShip_BringsItsOwnLimits()
    {
        var d = new Docked();
        d.Player.AssignShipType(new Shuttle());
        var runner = new Runner();
        d.Player.Credits = 100000;

        d.Universe.BuyShip(new Runner());

        Assert.Equal(runner.Limits.MaxMass, d.Player.ShipType.Limits.MaxMass);
        Assert.Equal(runner.Limits.MaxWeapons, d.Player.ShipType.Limits.MaxWeapons);
        Assert.Equal(runner.Flight.Radius, d.Player.Radius);
        d.Universe.Dispose();
    }

    [Fact]
    public void AShipTheBuyerCannotAfford_IsRefused_AndNothingChanges()
    {
        var d = new Docked();
        d.Player.AssignShipType(new Shuttle());
        int cost = d.Universe.ShipCostAfterTradeIn(new Runner());
        d.Player.Credits = cost - 1;
        var equipment = d.Player.Equipment.ToList();

        Assert.False(d.Universe.BuyShip(new Runner()));

        Assert.Equal(cost - 1, d.Player.Credits);
        Assert.Equal("Shuttle", d.Player.ShipType.ShipTypeName);
        Assert.Equal(equipment, d.Player.Equipment);
        Assert.Contains(d.Messages, m => m.Contains("Not enough credits"));
        d.Universe.Dispose();
    }

    [Fact]
    public void ATradeInWorthMoreThanTheNewShip_PaysThePlayerTheDifference()
    {
        var d = new Docked();
        d.Player.AssignShipType(new Runner());
        int cost = d.Universe.ShipCostAfterTradeIn(new Shuttle());
        Assert.True(cost < 0);
        d.Player.Credits = 10;

        Assert.True(d.Universe.BuyShip(new Shuttle()));

        Assert.Equal(10 - cost, d.Player.Credits);
        Assert.Equal("Shuttle", d.Player.ShipType.ShipTypeName);
        d.Universe.Dispose();
    }

    [Fact]
    public void TheEquipmentThePlayerBought_IsPartOfTheTradeIn()
    {
        var d = new Docked();
        d.Player.AssignShipType(new Shuttle());
        int costBefore = d.Universe.ShipCostAfterTradeIn(new Runner());
        d.Player.Credits = 1000;
        d.Player.ShipType.Limits.MaxWeapons = 5;
        d.Player.ShipType.Limits.MaxMass = 10000f;
        Assert.True(d.Universe.BuyEquipment(Gun(price: 120)));
        Assert.True(d.Universe.BuyEquipment(Gun(price: 120)));

        Assert.Equal(costBefore - 240, d.Universe.ShipCostAfterTradeIn(new Runner()));
        d.Universe.Dispose();
    }

    [Fact]
    public void BuyingTheSameTypeAgain_ResetsTheLoadoutAtTheDifferenceInValue()
    {
        var d = new Docked();
        d.Player.AssignShipType(new Shuttle());
        Assert.Equal(0, d.Universe.ShipCostAfterTradeIn(new Shuttle())); // an unmodified ship trades for an identical new one at no cost
        d.Player.Equipment.Add(new UtilityComponent { EquipmentName = "Extra", Price = 90 });
        d.Player.Credits = 0;

        Assert.True(d.Universe.BuyShip(new Shuttle()));

        Assert.Equal(90, d.Player.Credits); // the extra item was worth 90, and the new ship's loadout has no extra
        Assert.DoesNotContain(d.Player.Equipment, item => item.EquipmentName == "Extra");
        d.Universe.Dispose();
    }

    // -- what is saved -------------------------------------------------------------------------

    [Fact]
    public void AfterBuyingAShipAndEquipment_TheCreditsShipAndEquipmentSurviveASaveAndLoad()
    {
        string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"su-trade-{System.Guid.NewGuid():N}.json");
        try
        {
            var d = new Docked();
            d.Player.Credits = 100000;
            Assert.True(d.Universe.BuyShip(ShipStats.BuiltIn().First(type => type.ShipTypeName == "Runner")));
            d.Player.Equipment.Clear();
            d.Player.ShipType.Limits.MaxMass = 100000f;
            d.Player.ShipType.Limits.MaxUtilities = 2;
            Assert.True(d.Universe.BuyEquipment(EquipmentCatalog.All.First(item => item is UtilityComponent)));
            var names = d.Player.Equipment.Select(item => item.EquipmentName).ToList();
            int credits = d.Player.Credits;
            new FileSaveService(path).Save(d.Universe); // docking saves, and so does quitting
            d.Universe.Dispose();

            var loaded = new FileSaveService(path).LoadAll().Single();

            Assert.Equal(credits, loaded.Player.Credits);
            Assert.Equal("Runner", loaded.Player.ShipType.ShipTypeName);
            Assert.Equal(names, loaded.Player.Equipment.Select(item => item.EquipmentName));
            Assert.DoesNotContain(loaded.Player.Equipment, item => item is UnknownComponent);
        }
        finally { if (System.IO.File.Exists(path)) System.IO.File.Delete(path); }
    }
}
