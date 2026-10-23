using System.Linq;
using Strange_Universe.Game.Components.EquipmentComponents;
using Strange_Universe.Game.Components.ShipComponents;
using Strange_Universe.Game.Entities.ShipParts;
using Strange_Universe.Tests.Fakes;
using Xunit;

namespace Strange_Universe.Tests;

/// <summary>What equipment is worth, which kind of slot it takes, and the limits a ship type puts on what a ship can carry.</summary>
public class LoadoutLimitsTests
{
    /// <summary>A ship with exactly the given limits and no equipment, so a test does not depend on any ship type's numbers.</summary>
    private static Nonplayer ShipWithLimits(int weapons = 1, int shields = 1, int plating = 1, int engines = 1, int utilities = 1, float maxMass = 1000f)
    {
        var ship = TestGear.Arm(new Nonplayer("limits"));
        var limits = ship.ShipType.Limits;
        limits.MaxWeapons = weapons;
        limits.MaxShields = shields;
        limits.MaxPlating = plating;
        limits.MaxEngines = engines;
        limits.MaxUtilities = utilities;
        limits.MaxMass = maxMass;
        return ship;
    }

    private static WeaponComponent Gun(float mass = 10f, int price = 100) { var gun = TestGear.Gun(); gun.Mass = mass; gun.Price = price; return gun; }
    private static ShieldComponent Shield(float mass = 10f, int price = 100) { var shield = TestGear.Shield(); shield.Mass = mass; shield.Price = price; return shield; }

    // -- slot kinds ------------------------------------------------------------------------

    [Fact]
    public void EveryKindOfWeapon_TakesAWeaponSlot_AndTheOtherKindsTheirOwn()
    {
        foreach (var kind in new[] { EquipmentType.FixedProjectile, EquipmentType.TurretProjectile, EquipmentType.FixedBeam,
                                     EquipmentType.TurretBeam, EquipmentType.FixedMissile, EquipmentType.TurretMissile })
            Assert.Equal(SlotKind.Weapon, new WeaponComponent(kind).Slot);

        Assert.Equal(SlotKind.Shield, new ShieldComponent().Slot);
        Assert.Equal(SlotKind.Plating, new PlatingComponent().Slot);
        Assert.Equal(SlotKind.Engine, new EngineComponent().Slot);
        Assert.Equal(SlotKind.Utility, new UtilityComponent().Slot);
    }

    [Fact]
    public void AnItemThatIsNoLongerKnown_HasNoSlotKind_AndIsWorthNothing()
    {
        var unknown = new UnknownComponent("Retired Gizmo");

        Assert.Null(unknown.Slot);
        Assert.Equal(0, unknown.Price);
        Assert.Equal(0f, unknown.Mass);
    }

    [Fact]
    public void EveryCatalogItem_HasAPriceAndASlotKind()
    {
        foreach (var item in EquipmentCatalog.All)
        {
            Assert.True(item.Price > 0, $"{item.EquipmentName} has no price");
            Assert.NotNull(item.Slot);
        }
    }

    // -- counting, mass and value -----------------------------------------------------------

    [Fact]
    public void AShip_CountsItsEquipmentByKind_AndAddsUpItsMassAndValue()
    {
        var ship = TestGear.Arm(new Nonplayer("count"), Gun(mass: 30f, price: 100), Gun(mass: 20f, price: 150), Shield(mass: 50f, price: 200),
                                new UnknownComponent("Retired Gizmo"));

        Assert.Equal(2, ship.CountOf(SlotKind.Weapon));
        Assert.Equal(1, ship.CountOf(SlotKind.Shield));
        Assert.Equal(0, ship.CountOf(SlotKind.Engine));
        Assert.Equal(100f, ship.EquipmentMass);
        Assert.Equal(450, ship.EquipmentValue);
        Assert.Equal(ship.ShipType.Price + 450, ship.ShipValue);
    }

    // -- the limits ----------------------------------------------------------------------------

    [Fact]
    public void AFreeSlotOfTheKind_LetsItemsIn_UpToExactlyTheLimit()
    {
        var ship = ShipWithLimits(weapons: 2);

        Assert.Equal(InstallCheck.Ok, ship.Install(Gun()));
        Assert.Equal(InstallCheck.Ok, ship.Install(Gun()));   // exactly at the limit is allowed
        Assert.Equal(InstallCheck.NoSlot, ship.Install(Gun()));

        Assert.Equal(2, ship.CountOf(SlotKind.Weapon));       // the refused one was not added
    }

    [Fact]
    public void TheKinds_AreLimitedIndependently()
    {
        var ship = ShipWithLimits(weapons: 1, shields: 1);
        ship.Install(Gun());

        Assert.Equal(InstallCheck.NoSlot, ship.CheckInstall(Gun()));      // weapons are full...
        Assert.Equal(InstallCheck.Ok, ship.CheckInstall(Shield()));       // ...which does not block a shield
    }

    [Fact]
    public void ALimitOfZero_MeansTheShipCannotCarryThatKindAtAll()
    {
        var ship = ShipWithLimits(shields: 0);

        Assert.Equal(InstallCheck.NoSlot, ship.CheckInstall(Shield()));
    }

    [Fact]
    public void TheMassLimit_IsEnforced_ExactlyAtTheLimitIsAllowed()
    {
        var ship = ShipWithLimits(weapons: 5, maxMass: 100f);
        Assert.Equal(InstallCheck.Ok, ship.Install(Gun(mass: 60f)));

        Assert.Equal(InstallCheck.Ok, ship.CheckInstall(Gun(mass: 40f)));         // 100 of 100
        Assert.Equal(InstallCheck.TooHeavy, ship.CheckInstall(Gun(mass: 40.5f)));  // just over
        Assert.Equal(InstallCheck.TooHeavy, ship.Install(Gun(mass: 41f)));
        Assert.Equal(60f, ship.EquipmentMass);                                    // nothing was added
    }

    [Fact]
    public void ANoSlotAnswer_ComesBeforeATooHeavyOne()
    {
        var ship = ShipWithLimits(weapons: 0, maxMass: 1f);

        Assert.Equal(InstallCheck.NoSlot, ship.CheckInstall(Gun(mass: 500f)));
    }

    [Fact]
    public void AnItemWithNoSlotKind_CannotBeInstalled()
    {
        var ship = ShipWithLimits();

        Assert.Equal(InstallCheck.NoSlot, ship.CheckInstall(new UnknownComponent("Retired Gizmo")));
    }

    [Fact]
    public void RemovingEquipment_FreesItsSlotAndItsMass()
    {
        var ship = ShipWithLimits(weapons: 1, maxMass: 100f);
        var gun = Gun(mass: 90f);
        ship.Install(gun);
        Assert.Equal(InstallCheck.NoSlot, ship.CheckInstall(Gun(mass: 10f)));

        Assert.True(ship.Uninstall(gun));

        Assert.Equal(0, ship.CountOf(SlotKind.Weapon));
        Assert.Equal(InstallCheck.Ok, ship.CheckInstall(Gun(mass: 90f)));
        Assert.False(ship.Uninstall(gun)); // it is not there any more
    }

    [Fact]
    public void ALimitIsNotEnforcedOnAShipThatAlreadyHasMore()
    {
        // A loaded save or a default loadout is never trimmed: the limit only says no to adding more.
        var ship = TestGear.Arm(new Nonplayer("overfull"), Gun(), Gun(), Gun());
        ship.ShipType.Limits.MaxWeapons = 1;

        Assert.Equal(3, ship.CountOf(SlotKind.Weapon));
        Assert.Equal(InstallCheck.NoSlot, ship.CheckInstall(Gun()));
    }

    // -- the built-in ship types ----------------------------------------------------------------------

    [Fact]
    public void EveryShipTypes_DefaultLoadout_FitsItsOwnLimits()
    {
        foreach (var type in ShipStats.BuiltIn())
        {
            foreach (var kind in System.Enum.GetValues<SlotKind>())
            {
                int carried = type.DefaultLoadout.Count(item => item.Slot == kind);
                Assert.True(carried <= type.Limits.MaxOf(kind), $"{type.ShipTypeName}: {carried} {kind} items, room for {type.Limits.MaxOf(kind)}");
            }

            Assert.True(type.DefaultLoadout.Sum(item => item.Mass) <= type.Limits.MaxMass, $"{type.ShipTypeName}: its default loadout is too heavy for it");
            Assert.DoesNotContain(type.DefaultLoadout, item => item.Slot == null); // and nothing in it is an unknown item
        }
    }

    [Fact]
    public void EveryShipType_HasAPrice_AndAListPriceThatIncludesItsLoadout()
    {
        foreach (var type in ShipStats.BuiltIn())
        {
            Assert.True(type.Price > 0, type.ShipTypeName);
            Assert.True(type.Limits.MaxMass > 0f, type.ShipTypeName);
            Assert.Equal(type.Price + type.DefaultLoadout.Sum(item => item.Price), type.ListPrice);
            Assert.True(type.ListPrice > type.Price);
        }
    }

    [Fact]
    public void EveryBuiltInShipType_IsFoundByName_AndEachNameIsDifferent()
    {
        var types = ShipStats.BuiltIn();

        Assert.Equal(types.Count, types.Select(type => type.ShipTypeName).Distinct().Count());
        foreach (var type in types)
            Assert.Equal(type.ShipTypeName, ShipStats.FromName(type.ShipTypeName.ToLowerInvariant()).ShipTypeName);
    }

    [Fact]
    public void ANewShipsCurrentWorth_IsItsTypePlusItsDefaultLoadout()
    {
        foreach (var type in ShipStats.BuiltIn())
        {
            var ship = new Nonplayer("fresh", type);

            Assert.Equal(type.ListPrice, ship.ShipValue);
        }
    }
}
