using System.Linq;
using Microsoft.Xna.Framework;
using Strange_Universe.Game.Components;
using Strange_Universe.Game.Components.EquipmentComponents;
using Strange_Universe.Game.Components.PlanetComponents;
using Strange_Universe.Game.Components.ShipComponents;
using Strange_Universe.Game.Entities;
using Strange_Universe.Game.Entities.ShipParts;
using Strange_Universe.Game.UI;
using Strange_Universe.Tests.Fakes;
using Xunit;
using static Strange_Universe.Tests.Fakes.DockedWorld;

namespace Strange_Universe.Tests;

/// <summary>The equipment bay and the starship bay: stock lists, copies for buyers, their rows, and the store screen's navigation.</summary>
public class BayTests
{
    private static EquipmentBayService EquipmentBay(Planet planet) => planet.Spaceport.Services.OfType<EquipmentBayService>().Single();
    private static StarshipBayService StarshipBay(Planet planet) => planet.Spaceport.Services.OfType<StarshipBayService>().Single();

    // -- cloning ------------------------------------------------------------------------------------

    [Fact]
    public void AClonedItem_IsAnotherObject_WithTheSameStats()
    {
        foreach (var item in EquipmentCatalog.All)
        {
            var copy = item.Clone();

            Assert.NotSame(item, copy);
            Assert.Equal(item.GetType(), copy.GetType());
            Assert.Equal(item.EquipmentName, copy.EquipmentName);
            Assert.Equal(item.Price, copy.Price);
            Assert.Equal(item.Mass, copy.Mass);
            Assert.Equal(item.EquipmentType, copy.EquipmentType);
        }
    }

    [Fact]
    public void AClonedWeapon_IsReadyToFire_AndHasItsOwnLook()
    {
        var weapon = (WeaponComponent)EquipmentCatalog.All.First(item => item is WeaponComponent { IsPrimary: true });
        weapon.TryFire(new Nonplayer("shooter"));
        Assert.False(weapon.CanFire);

        var copy = (WeaponComponent)weapon.Clone();

        Assert.True(copy.CanFire);                                   // a copy is as new, whatever the original has been through
        Assert.NotSame(weapon.Visual, copy.Visual);
        Assert.Equal(weapon.Visual.BurstDuration, copy.Visual.BurstDuration);
        Assert.Equal(weapon.ImpactLingerSeconds, copy.ImpactLingerSeconds);
        copy.TryFire(new Nonplayer("shooter"));
        Assert.False(copy.CanFire);
        Assert.False(weapon.CanFire);                                // each has its own cooldown
    }

    [Fact]
    public void AClonedShield_IsFull_EvenIfTheOriginalHasBeenHit()
    {
        var shield = (ShieldComponent)EquipmentCatalog.All.First(item => item is ShieldComponent);
        shield.Absorb(shield.ShieldStrength);
        Assert.True(shield.IsDown);

        var copy = (ShieldComponent)shield.Clone();

        Assert.Equal(copy.ShieldStrength, copy.Strength);
        Assert.False(copy.IsDown);
        Assert.True(shield.IsDown);
    }

    [Fact]
    public void AClonedShipType_IsTheSameKind_WithItsOwnPartsAndLoadout()
    {
        foreach (var type in ShipStats.BuiltIn())
        {
            var copy = type.Clone();

            Assert.NotSame(type, copy);
            Assert.Equal(type.GetType(), copy.GetType());
            Assert.Equal(type.ShipTypeName, copy.ShipTypeName);
            Assert.Equal(type.ListPrice, copy.ListPrice);
            Assert.NotSame(type.Flight, copy.Flight);
            Assert.NotSame(type.Limits, copy.Limits);
            Assert.Equal(type.DefaultLoadout.Count, copy.DefaultLoadout.Count);
            for (int i = 0; i < type.DefaultLoadout.Count; i++)
                Assert.NotSame(type.DefaultLoadout[i], copy.DefaultLoadout[i]);
        }
    }

    // -- the bays on a planet ---------------------------------------------------------------------------

    [Fact]
    public void EverySpaceport_HasBothBays_WithTheCatalogsAsStock()
    {
        var planet = PlanetWith(spaceport: true);

        Assert.Equal(EquipmentCatalog.All.Select(item => item.EquipmentName).OrderBy(name => name), EquipmentBay(planet).Stock.Select(item => item.EquipmentName).OrderBy(name => name));
        Assert.Equal(ShipStats.BuiltIn().Select(type => type.ShipTypeName).OrderBy(name => name), StarshipBay(planet).Stock.Select(type => type.ShipTypeName).OrderBy(name => name));
    }

    // -- grade and the order of the lists ---------------------------------------------------------------

    [Fact]
    public void EveryItemAndShipType_HasAGrade()
    {
        Assert.All(EquipmentCatalog.All, item => Assert.True(item.Grade >= 1, item.EquipmentName));
        Assert.All(ShipStats.BuiltIn(), type => Assert.True(type.Grade >= 1, type.ShipTypeName));
    }

    [Fact]
    public void AClonedItemOrShip_KeepsItsGrade()
    {
        foreach (var item in EquipmentCatalog.All)
            Assert.Equal(item.Grade, item.Clone().Grade);
        foreach (var type in ShipStats.BuiltIn())
            Assert.Equal(type.Grade, type.Clone().Grade);
    }

    [Fact]
    public void TheEquipmentBayLists_ByKindOfEquipmentFirst_ThenByGrade_TheHighestLast()
    {
        // Handed over in the worst order: the bay sorts them.
        var jumbled = EquipmentCatalog.All.OrderByDescending(item => item.Grade).ThenByDescending(item => item.EquipmentType).ToList();

        var stock = new EquipmentBayService(jumbled).Stock;

        for (int i = 1; i < stock.Count; i++)
        {
            var before = stock[i - 1];
            var after = stock[i];
            Assert.True(before.EquipmentType <= after.EquipmentType, $"{before.EquipmentName} is listed before {after.EquipmentName}");
            if (before.EquipmentType == after.EquipmentType)
                Assert.True(before.Grade <= after.Grade, $"{before.EquipmentName} (grade {before.Grade}) is listed before {after.EquipmentName} (grade {after.Grade})");
        }
        Assert.Equal(jumbled.Count, stock.Count); // nothing lost or added
    }

    [Fact]
    public void TheListsOnAPlanet_AreSortedToo_AndTheRowsFollowTheStock()
    {
        using var u = Docked(out var planet);
        var bay = EquipmentBay(planet);
        var rows = bay.Rows(u);

        for (int i = 0; i < bay.Stock.Count; i++)
            Assert.StartsWith(bay.Stock[i].EquipmentName, rows[i]);                  // row i is stock item i, so choosing row i buys it
        Assert.Contains($"G{bay.Stock[0].Grade}", rows[0]);                          // and each row shows its grade

        var ships = StarshipBay(planet).Stock;
        for (int i = 1; i < ships.Count; i++)
            Assert.True(ships[i - 1].Grade <= ships[i].Grade);
    }

    [Fact]
    public void TheSellLines_AreInTheSameOrder()
    {
        using var u = Docked(out var planet);
        var bay = EquipmentBay(planet);
        var high = new ShieldComponent { EquipmentName = "High Shield", Grade = 3, Price = 10 };
        var low = new ShieldComponent { EquipmentName = "Low Shield", Grade = 1, Price = 10 };
        var gun = TestGear.Gun(); gun.EquipmentName = "Some Gun"; gun.Grade = 2;
        TestGear.Arm(u.Player, high, low, gun);

        var sell = bay.Rows(u).Skip(bay.Stock.Count).ToList();

        Assert.Equal(new[] { "Sell Some Gun", "Sell Low Shield", "Sell High Shield" }, sell.Select(row => row.Substring(0, row.IndexOf("   ")).Trim()));
    }

    // -- the equipment bay ---------------------------------------------------------------------------------

    [Fact]
    public void TheEquipmentBayRows_ListEachStockItemWithItsPrice_ThenWhatThePlayerCanSell()
    {
        using var u = Docked(out var planet);
        var bay = EquipmentBay(planet);
        TestGear.Arm(u.Player, new UtilityComponent { EquipmentName = "Carried Thing", Price = 77 }, new UnknownComponent("Retired Gizmo"));

        var rows = bay.Rows(u);

        for (int i = 0; i < bay.Stock.Count; i++)
        {
            Assert.Contains(bay.Stock[i].EquipmentName, rows[i]);
            Assert.Contains($"{bay.Stock[i].Price} cr", rows[i]);
        }
        Assert.Equal(bay.Stock.Count + 1, rows.Count); // one sell row: the unknown item cannot be sold
        Assert.Contains("Sell Carried Thing", rows.Last());
        Assert.Contains("77 cr", rows.Last());
    }

    [Fact]
    public void ARowForAKindOfEquipment_ShowsHowManyTheShipCarriesAgainstItsLimit()
    {
        using var u = Docked(out var planet);
        var bay = EquipmentBay(planet);
        TestGear.Arm(u.Player, TestGear.Gun());
        u.Player.ShipType.Limits.MaxWeapons = 3;
        int row = bay.Stock.FindIndex(item => item is WeaponComponent);

        Assert.Contains("[1/3]", bay.Rows(u)[row]);
    }

    [Fact]
    public void ChoosingABuyRow_BuysACopy_AndTheStockIsUntouched()
    {
        using var u = Docked(out var planet);
        var bay = EquipmentBay(planet);
        int stockCount = bay.Stock.Count;
        TestGear.Arm(u.Player);
        u.Player.Credits = 100000;
        u.Player.ShipType.Limits.MaxWeapons = 3;
        u.Player.ShipType.Limits.MaxMass = 100000f;
        int row = bay.Stock.FindIndex(item => item is WeaponComponent);
        var onShelf = bay.Stock[row];

        bay.Choose(u, row);
        bay.Choose(u, row);

        Assert.Equal(2, u.Player.Equipment.Count);
        Assert.Equal(onShelf.EquipmentName, u.Player.Equipment[0].EquipmentName);
        Assert.NotSame(u.Player.Equipment[0], u.Player.Equipment[1]);
        Assert.DoesNotContain(u.Player.Equipment, item => ReferenceEquals(item, onShelf)); // the shelf item itself is never handed over
        Assert.Equal(stockCount, bay.Stock.Count);
        Assert.Equal(100000 - 2 * onShelf.Price, u.Player.Credits);
    }

    [Fact]
    public void ChoosingASellRow_SellsThatItem()
    {
        using var u = Docked(out var planet);
        var bay = EquipmentBay(planet);
        var carried = new UtilityComponent { EquipmentName = "Carried Thing", Price = 77 };
        TestGear.Arm(u.Player, carried);
        u.Player.Credits = 0;

        bay.Choose(u, bay.Stock.Count); // the first row after the stock

        Assert.Empty(u.Player.Equipment);
        Assert.Equal(77, u.Player.Credits);
    }

    [Fact]
    public void ChoosingARowThatDoesNotExist_DoesNothing()
    {
        using var u = Docked(out var planet);
        var bay = EquipmentBay(planet);
        TestGear.Arm(u.Player);
        u.Player.Credits = 500;

        bay.Choose(u, -1);
        bay.Choose(u, bay.Stock.Count + 50);

        Assert.Equal(500, u.Player.Credits);
        Assert.Empty(u.Player.Equipment);
    }

    // -- the starship bay ------------------------------------------------------------------------------------

    [Fact]
    public void TheStarshipBayRows_ShowEachShipsListPrice_TheTradeInAndTheCost()
    {
        using var u = Docked(out var planet);
        var bay = StarshipBay(planet);

        var rows = bay.Rows(u);

        Assert.Equal(bay.Stock.Count, rows.Count);
        for (int i = 0; i < rows.Count; i++)
        {
            Assert.Contains(bay.Stock[i].ShipTypeName, rows[i]);
            Assert.Contains($"{bay.Stock[i].ListPrice} cr", rows[i]);
            Assert.Contains($"trade-in {u.Player.ShipValue} cr", rows[i]);
            Assert.Contains($"cost {u.ShipCostAfterTradeIn(bay.Stock[i])} cr", rows[i]);
        }
    }

    [Fact]
    public void ChoosingAShipRow_BuysACopyOfTheStockType_AndTheStockIsUntouched()
    {
        using var u = Docked(out var planet);
        var bay = StarshipBay(planet);
        u.Player.AssignShipType(new Shuttle());
        u.Player.Credits = 100000;
        int row = bay.Stock.FindIndex(type => type.ShipTypeName == "Runner");
        var onShelf = bay.Stock[row];

        bay.Choose(u, row);

        Assert.Equal("Runner", u.Player.ShipType.ShipTypeName);
        Assert.NotSame(onShelf, u.Player.ShipType);
        Assert.DoesNotContain(u.Player.Equipment, item => onShelf.DefaultLoadout.Any(shelved => ReferenceEquals(shelved, item)));
        Assert.Equal(ShipStats.BuiltIn().Count, bay.Stock.Count);
    }

    // -- the store screen --------------------------------------------------------------------------------------

    private static int Index(SpaceportMenu menu, string name) => menu.Items.ToList().FindIndex(service => service.Name == name);

    private static void Open(SpaceportMenu menu, Universe u, string name)
    {
        while (menu.Items[menu.Selected].Name != name)
            menu.Update(Down);
        menu.Update(Select);
    }

    [Fact]
    public void ChoosingAStore_OpensItsRows_InsteadOfDoingAnything()
    {
        using var u = Docked(out var planet);
        var menu = new SpaceportMenu(planet, u);
        u.Player.Credits = 1000;

        Open(menu, u, "Equipment");

        Assert.Same(EquipmentBay(planet), menu.Open);
        Assert.Equal("Equipment", menu.Heading);
        Assert.Equal(EquipmentBay(planet).Rows(u), menu.Lines);
        Assert.Equal(0, menu.Selected);
        Assert.Equal(1000, u.Player.Credits);   // opening it bought nothing
        Assert.True(u.IsDocked);
    }

    [Fact]
    public void InAStore_UpDownAndSelectWorkOnTheRows()
    {
        using var u = Docked(out var planet);
        var menu = new SpaceportMenu(planet, u);
        TestGear.Arm(u.Player);
        u.Player.Credits = 100000;
        u.Player.ShipType.Limits.MaxMass = 100000f;
        u.Player.ShipType.Limits.MaxWeapons = 9; u.Player.ShipType.Limits.MaxShields = 9; u.Player.ShipType.Limits.MaxPlating = 9;
        u.Player.ShipType.Limits.MaxEngines = 9; u.Player.ShipType.Limits.MaxUtilities = 9;
        Open(menu, u, "Equipment");
        int rows = menu.Lines.Count;

        menu.Update(Up);
        Assert.Equal(rows - 1, menu.Selected); // wrapped to the last row (there is nothing to sell yet, so the last stock item)

        menu.Update(Down);
        menu.Update(Down);
        Assert.Equal(1, menu.Selected);
        menu.Update(Select);                // buys the second stock item

        Assert.Equal(EquipmentBay(planet).Stock[1].EquipmentName, Assert.Single(u.Player.Equipment).EquipmentName);
        Assert.True(menu.Lines.Count > rows);  // and it now appears as a row to sell
    }

    [Fact]
    public void Back_ClosesTheStore_AndPutsTheHighlightBackOnIt()
    {
        using var u = Docked(out var planet);
        var menu = new SpaceportMenu(planet, u);
        Open(menu, u, "Starships");
        int starships = Index(menu, "Starships") >= 0 ? Index(menu, "Starships") : 0;
        menu.Update(Down); // move inside the store

        Assert.True(menu.Back());

        Assert.Null(menu.Open);
        Assert.Null(menu.Heading);
        Assert.Equal("Starships", menu.Items[menu.Selected].Name);
        Assert.Equal(starships, menu.Selected);
    }

    [Fact]
    public void Back_WithNoStoreOpen_DoesNothing_SoEscapeCanLeaveInstead()
    {
        using var u = Docked(out var planet);
        var menu = new SpaceportMenu(planet, u);

        Assert.False(menu.Back());
        Assert.Null(menu.Open);
    }

    [Fact]
    public void WhileAStoreIsOpen_TheServicesAreNotChosen()
    {
        using var u = Docked(out var planet);
        var menu = new SpaceportMenu(planet, u);
        Open(menu, u, "Equipment");
        u.Player.Credits = 0;

        menu.Update(Select); // a buy row with no credits: refused, and the menu is still the store

        Assert.True(u.IsDocked);
        Assert.NotNull(menu.Open);
    }
}
