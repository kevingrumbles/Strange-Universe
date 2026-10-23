using System.Linq;
using Microsoft.Xna.Framework;
using Strange_Universe.Game.Components;
using Strange_Universe.Game.Components.EquipmentComponents;
using Strange_Universe.Game.Components.PlanetComponents;
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
        public RecordingAssetRequests Assets { get; } = new();
        public Player Player => Universe.Player;

        public Docked(bool docked = true)
        {
            Universe = new Universe("Trader", "trade-seed") { NebulaFactory = Nebula.CreateWithoutPixels };
            Universe.Generate(Assets);
            Player.Navigator.Clear();

            var planet = Enumerable.Range(0, 1000).Select(i => new Planet($"trade_Planet_{i}", 90f, 200f, 3000f, 9000f, 1 + i % 8, 8))
                                   .First(p => p.Spaceport != null);
            Universe.ActiveStarSystem.Planets.Clear();
            Universe.ActiveStarSystem.Planets.Add(planet);
            Player.Position = planet.Position + new Vector2(planet.Radius + 20f, 0f);
            if (docked) Universe.Dock(planet);
        }
    }

    private static EquipmentOffer GunOffer(int price = 200, float mass = 10f) =>
        new(() => { var gun = TestGear.Gun(); gun.Price = price; gun.Mass = mass; return gun; });

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
    public void BuyingEquipment_SpendsThePrice_AndInstallsANewItem()
    {
        var d = new Docked();
        Prepare(d, credits: 500);
        var offer = GunOffer(price: 200);

        var result = d.Universe.BuyEquipment(offer);

        Assert.Equal(TradeResult.Ok, result);
        Assert.Equal(300, d.Player.Credits);
        var bought = Assert.Single(d.Player.Equipment);
        Assert.NotSame(offer.Template, bought); // the template on the shelf is not the item
        d.Universe.Dispose();
    }

    [Fact]
    public void BuyingTheSameOfferTwice_GivesTwoSeparateItems()
    {
        var d = new Docked();
        Prepare(d, credits: 1000, weapons: 2);
        var offer = GunOffer();

        d.Universe.BuyEquipment(offer);
        d.Universe.BuyEquipment(offer);

        Assert.Equal(2, d.Player.Equipment.Count);
        Assert.NotSame(d.Player.Equipment[0], d.Player.Equipment[1]);
        d.Universe.Dispose();
    }

    [Fact]
    public void WithJustEnoughCredits_TheBuyGoesThrough_LeavingNone()
    {
        var d = new Docked();
        Prepare(d, credits: 200);

        Assert.Equal(TradeResult.Ok, d.Universe.BuyEquipment(GunOffer(price: 200)));

        Assert.Equal(0, d.Player.Credits);
        d.Universe.Dispose();
    }

    [Fact]
    public void TooFewCredits_RefusesAndChangesNothing()
    {
        var d = new Docked();
        Prepare(d, credits: 199);

        var result = d.Universe.BuyEquipment(GunOffer(price: 200));

        Assert.Equal(TradeResult.NotEnoughCredits, result);
        Assert.Equal(199, d.Player.Credits);
        Assert.Empty(d.Player.Equipment);
        d.Universe.Dispose();
    }

    [Fact]
    public void NoFreeSlot_RefusesAndChangesNothing()
    {
        var d = new Docked();
        Prepare(d, credits: 1000, weapons: 1, gear: GunOffer().Create());

        var result = d.Universe.BuyEquipment(GunOffer());

        Assert.Equal(TradeResult.NoSlot, result);
        Assert.Equal(1000, d.Player.Credits);
        Assert.Single(d.Player.Equipment);
        d.Universe.Dispose();
    }

    [Fact]
    public void TooHeavy_RefusesAndChangesNothing_ButExactlyTheLimitIsFine()
    {
        var d = new Docked();
        Prepare(d, credits: 1000, weapons: 3, maxMass: 100f, gear: GunOffer(mass: 60f).Create());

        Assert.Equal(TradeResult.TooHeavy, d.Universe.BuyEquipment(GunOffer(mass: 41f)));
        Assert.Equal(1000, d.Player.Credits);
        Assert.Single(d.Player.Equipment);

        Assert.Equal(TradeResult.Ok, d.Universe.BuyEquipment(GunOffer(mass: 40f))); // 100 of 100
        d.Universe.Dispose();
    }

    [Fact]
    public void CreditsAreCheckedBeforeWhetherItFits()
    {
        var d = new Docked();
        Prepare(d, credits: 10, weapons: 0); // cannot afford it and has no slot for it

        Assert.Equal(TradeResult.NotEnoughCredits, d.Universe.BuyEquipment(GunOffer(price: 200)));
        d.Universe.Dispose();
    }

    [Fact]
    public void WhenNotDocked_NothingCanBeBoughtOrSold()
    {
        var d = new Docked(docked: false);
        Prepare(d, credits: 5000, weapons: 2);
        var owned = GunOffer().Create();
        d.Player.Equipment.Add(owned);

        Assert.Equal(TradeResult.NotDocked, d.Universe.BuyEquipment(GunOffer()));
        Assert.Equal(TradeResult.NotDocked, d.Universe.BuyShip(new ShipOffer(() => new Runner())));
        Assert.Equal(TradeResult.NotDocked, d.Universe.SellEquipment(owned));

        Assert.Equal(5000, d.Player.Credits);
        Assert.Single(d.Player.Equipment);
        d.Universe.Dispose();
    }

    // -- selling equipment -------------------------------------------------------------------

    [Fact]
    public void SellingEquipment_RemovesIt_AndPaysItsPrice()
    {
        var d = new Docked();
        var gun = GunOffer(price: 350).Create();
        Prepare(d, credits: 100, gear: gun);

        var result = d.Universe.SellEquipment(gun);

        Assert.Equal(TradeResult.Ok, result);
        Assert.Equal(450, d.Player.Credits);
        Assert.Empty(d.Player.Equipment);
        d.Universe.Dispose();
    }

    [Fact]
    public void SellingSomethingTheShipDoesNotCarry_ChangesNothing()
    {
        var d = new Docked();
        Prepare(d, credits: 100);

        var result = d.Universe.SellEquipment(GunOffer().Create());

        Assert.Equal(TradeResult.NotCarried, result);
        Assert.Equal(100, d.Player.Credits);
        d.Universe.Dispose();
    }

    [Fact]
    public void SellingThenBuyingBack_CostsNothingInTheEnd_AndFreesTheSlotMeanwhile()
    {
        var d = new Docked();
        var offer = GunOffer(price: 300);
        var gun = offer.Create();
        Prepare(d, credits: 350, weapons: 1, gear: gun);
        Assert.Equal(TradeResult.NoSlot, d.Universe.BuyEquipment(offer)); // full

        d.Universe.SellEquipment(gun);
        Assert.Equal(TradeResult.Ok, d.Universe.BuyEquipment(offer));

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
        var offer = new ShipOffer(() => new Runner());
        int worth = d.Player.ShipType.Price + d.Player.EquipmentValue;
        int cost = offer.ListPrice - worth;
        Assert.True(cost > 0);
        d.Player.Credits = cost + 7;
        Assert.Equal(cost, d.Universe.ShipCostAfterTradeIn(offer)); // the price shown is the price charged

        var result = d.Universe.BuyShip(offer);

        Assert.Equal(TradeResult.Ok, result);
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
        var offer = new ShipOffer(() => new Runner());
        d.Player.Credits = 100000;

        d.Universe.BuyShip(offer);

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
    public void ABoughtShip_BringsItsOwnLimits_AndItsArtIsRequested()
    {
        var d = new Docked();
        d.Player.AssignShipType(new Shuttle());
        var runner = new Runner();
        d.Player.Credits = 100000;

        d.Universe.BuyShip(new ShipOffer(() => new Runner()));

        Assert.Equal(runner.Limits.MaxMass, d.Player.ShipType.Limits.MaxMass);
        Assert.Equal(runner.Limits.MaxWeapons, d.Player.ShipType.Limits.MaxWeapons);
        Assert.Equal(runner.Flight.Radius, d.Player.Radius);
        Assert.Contains(d.Assets.Calls, call => call.Method == nameof(IAssetRequests.EnsureShipArt) && call.Id == "Runner");
        d.Universe.Dispose();
    }

    [Fact]
    public void AShipTheBuyerCannotAfford_IsRefused_AndNothingChanges()
    {
        var d = new Docked();
        d.Player.AssignShipType(new Shuttle());
        var offer = new ShipOffer(() => new Runner());
        int cost = offer.ListPrice - d.Player.ShipValue;
        d.Player.Credits = cost - 1;
        var equipment = d.Player.Equipment.ToList();

        var result = d.Universe.BuyShip(offer);

        Assert.Equal(TradeResult.NotEnoughCredits, result);
        Assert.Equal(cost - 1, d.Player.Credits);
        Assert.Equal("Shuttle", d.Player.ShipType.ShipTypeName);
        Assert.Equal(equipment, d.Player.Equipment);
        d.Universe.Dispose();
    }

    [Fact]
    public void ATradeInWorthMoreThanTheNewShip_PaysThePlayerTheDifference()
    {
        var d = new Docked();
        d.Player.AssignShipType(new Runner());
        var offer = new ShipOffer(() => new Shuttle());
        int cost = offer.ListPrice - d.Player.ShipValue;
        Assert.True(cost < 0);
        d.Player.Credits = 10;

        var result = d.Universe.BuyShip(offer);

        Assert.Equal(TradeResult.Ok, result);
        Assert.Equal(10 - cost, d.Player.Credits);
        Assert.Equal("Shuttle", d.Player.ShipType.ShipTypeName);
        d.Universe.Dispose();
    }

    [Fact]
    public void TheEquipmentThePlayerBought_IsPartOfTheTradeIn()
    {
        var d = new Docked();
        d.Player.AssignShipType(new Shuttle());
        var offer = new ShipOffer(() => new Runner());
        int costBefore = d.Universe.ShipCostAfterTradeIn(offer);
        d.Player.Credits = 1000;
        d.Player.ShipType.Limits.MaxWeapons = 5;
        d.Player.ShipType.Limits.MaxMass = 10000f;
        Assert.Equal(TradeResult.Ok, d.Universe.BuyEquipment(GunOffer(price: 120)));
        Assert.Equal(TradeResult.Ok, d.Universe.BuyEquipment(GunOffer(price: 120)));

        Assert.Equal(costBefore - 240, d.Universe.ShipCostAfterTradeIn(offer));
        d.Universe.Dispose();
    }

    [Fact]
    public void BuyingTheSameTypeAgain_ResetsTheLoadoutAtTheDifferenceInValue()
    {
        var d = new Docked();
        d.Player.AssignShipType(new Shuttle());
        var offer = new ShipOffer(() => new Shuttle());
        Assert.Equal(0, d.Universe.ShipCostAfterTradeIn(offer)); // an unmodified ship trades for an identical new one at no cost
        d.Player.Equipment.Add(new UtilityComponent { EquipmentName = "Extra", Price = 90 });
        d.Player.Credits = 0;

        Assert.Equal(TradeResult.Ok, d.Universe.BuyShip(offer));

        Assert.Equal(90, d.Player.Credits); // the extra item was worth 90, and the new ship's loadout has no extra
        Assert.DoesNotContain(d.Player.Equipment, item => item.EquipmentName == "Extra");
        d.Universe.Dispose();
    }
}
