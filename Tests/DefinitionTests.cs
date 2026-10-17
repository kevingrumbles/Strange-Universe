using System.Linq;
using Strange_Universe.Game.Components.EquipmentComponents;
using Strange_Universe.Game.Entities.ShipParts;
using Xunit;

namespace Strange_Universe.Tests;

/// <summary>Every item of equipment is an ordinary preset defined in code, as a subclass for its kind.</summary>
public class DefinitionTests
{
    [Fact]
    public void EveryCatalogItem_CanBeCreatedByName_AsItsOwnKind()
    {
        foreach (var item in EquipmentCatalog.All)
        {
            var created = EquipmentComponent.Create(item.EquipmentName);
            Assert.Equal(item.GetType(), created.GetType());
            Assert.Equal(item.EquipmentName, created.EquipmentName);
        }

        // Other equipment is looked up the same way, and is the class for its kind.
        var shield = Assert.IsType<ShieldComponent>(EquipmentComponent.Create("Basic Shield Generator"));
        Assert.Equal(EquipmentType.Shield, shield.EquipmentType);
        Assert.IsType<PlatingComponent>(EquipmentComponent.Create("Reinforced Hull"));
        Assert.IsType<EngineComponent>(EquipmentComponent.Create("Standard Engine"));
        Assert.IsType<UtilityComponent>(EquipmentComponent.Create("Sensor Array"));
        Assert.IsType<UnknownComponent>(EquipmentComponent.Create("No Such Thing"));
    }

    [Fact]
    public void EveryCatalogItem_IsTheClassForItsType()
    {
        foreach (var item in EquipmentCatalog.All)
        {
            switch (item.EquipmentType)
            {
                case EquipmentType.Shield: Assert.IsType<ShieldComponent>(item); break;
                case EquipmentType.Hull: Assert.IsType<PlatingComponent>(item); break;
                case EquipmentType.Engine: Assert.IsType<EngineComponent>(item); break;
                case EquipmentType.Utility: Assert.IsType<UtilityComponent>(item); break;
                default: Assert.IsType<WeaponComponent>(item); break; // all the weapon kinds, missiles included
            }
        }
    }

    [Fact]
    public void MissilesAreWeapons_ButNotPrimaryWeapons_AndTheOtherKindsAre()
    {
        foreach (var weapon in EquipmentCatalog.All.OfType<WeaponComponent>())
        {
            bool missile = weapon.EquipmentType is EquipmentType.FixedMissile or EquipmentType.TurretMissile;
            Assert.Equal(!missile, weapon.IsPrimary);
        }
    }

    [Fact]
    public void AWeapon_CanOnlyBeCreatedWithAWeaponKind()
    {
        Assert.Throws<System.ArgumentException>(() => new WeaponComponent(EquipmentType.Shield));
        Assert.Throws<System.ArgumentException>(() => new WeaponComponent(EquipmentType.Unkown));
    }

    [Fact]
    public void EveryPrimaryWeapon_HasARangeAndDamage_AndItsOwnVisual_WhoseBurstIsHowLongItsProjectilesLinger()
    {
        var visuals = new System.Collections.Generic.List<ProjectileVisual>();
        foreach (var weapon in EquipmentCatalog.All.OfType<WeaponComponent>().Where(w => w.IsPrimary))
        {
            Assert.True(weapon.Range > 0f, weapon.EquipmentName);
            Assert.True(weapon.Damage > 0, weapon.EquipmentName);
            Assert.NotNull(weapon.Visual);
            Assert.Equal(weapon.Visual.BurstDuration, weapon.ImpactLingerSeconds); // the burst plays for as long as the projectile lingers
            visuals.Add(weapon.Visual);
        }

        Assert.Equal(visuals.Count, visuals.Distinct().Count()); // nothing shares a visual with another weapon
    }

    [Fact]
    public void AFiredProjectile_CarriesItsWeaponsVisual()
    {
        var gun = new WeaponComponent(EquipmentType.FixedProjectile)
        {
            Range = 1000f,
            Visual = new ProjectileVisual { BurstDuration = 0.5f, ParticleCount = 3 },
        };

        var shot = gun.TryFire(new Nonplayer("shooter"));

        Assert.Same(gun.Visual, shot.Visual);
        Assert.Equal(0.5f, shot.ImpactLingerSeconds); // and lingers for its burst
    }

    [Fact]
    public void EnergyCost_BelongsToTheKindsThatUseIt()
    {
        Assert.Null(typeof(EquipmentComponent).GetProperty("EnergyCost"));
        Assert.Null(typeof(PlatingComponent).GetProperty("EnergyCost"));
        Assert.Null(typeof(EngineComponent).GetProperty("EnergyCost"));
        Assert.NotNull(typeof(WeaponComponent).GetProperty("EnergyCost"));
        Assert.NotNull(typeof(ShieldComponent).GetProperty("EnergyCost"));
        Assert.NotNull(typeof(UtilityComponent).GetProperty("EnergyCost"));
    }
}
