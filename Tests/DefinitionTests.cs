using System.Linq;
using Microsoft.Xna.Framework;
using Strange_Universe.Game.Components;
using Strange_Universe.Game.Systems;
using Xunit;

namespace Strange_Universe.Tests;

/// <summary>Every item of equipment is an ordinary preset defined in code, as a subclass for its kind.</summary>
public class DefinitionTests
{
    private static readonly string[] WeaponNames =
        { "Light Laser", "Heavy Laser", "Pulse Cannon", "Gatling Gun", "Plasma Beam", "Mining Beam" };

    [Fact]
    public void Weapons_AreInTheCatalog_LikeAnyOtherEquipment()
    {
        foreach (var name in WeaponNames)
        {
            var weapon = Assert.IsType<Weapon>(Equipment.Create(name));
            Assert.Equal(name, weapon.EquipmentName);
            Assert.True(weapon.IsPrimary, name);
            Assert.Contains(EquipmentCatalog.All, e => e.EquipmentName == name);
        }

        // Other equipment is looked up the same way, and is the class for its kind.
        var shield = Assert.IsType<Shield>(Equipment.Create("Basic Shield Generator"));
        Assert.Equal(EquipmentType.Shield, shield.EquipmentType);
        Assert.Equal(25f, shield.ShieldStrength);
        Assert.Equal(5f, shield.RechargeRate);
        Assert.IsType<HullPlating>(Equipment.Create("Reinforced Hull"));
        Assert.IsType<Engine>(Equipment.Create("Standard Engine"));
        Assert.IsType<UtilityModule>(Equipment.Create("Sensor Array"));
        Assert.IsType<UnknownEquipment>(Equipment.Create("No Such Thing"));
    }

    [Fact]
    public void EveryCatalogItem_IsTheClassForItsType()
    {
        foreach (var item in EquipmentCatalog.All)
        {
            switch (item.EquipmentType)
            {
                case EquipmentType.Shield: Assert.IsType<Shield>(item); break;
                case EquipmentType.Hull: Assert.IsType<HullPlating>(item); break;
                case EquipmentType.Engine: Assert.IsType<Engine>(item); break;
                case EquipmentType.Utility: Assert.IsType<UtilityModule>(item); break;
                default: Assert.IsType<Weapon>(item); break; // all the weapon kinds, missiles included
            }
        }
    }

    [Fact]
    public void LightLaser_HasItsStats()
    {
        var light = Assert.IsType<Weapon>(Equipment.Create("light laser")); // names are not case sensitive
        Assert.Equal(EquipmentType.FixedProjectile, light.EquipmentType);
        Assert.Equal(15, light.Damage);
        Assert.Equal(2000f, light.ProjectileSpeed);
        Assert.Equal(1500f, light.Range);
        Assert.Equal(3f, light.FireRate);
        Assert.Equal(0.16f, light.ImpactLingerSeconds);
    }

    [Fact]
    public void Missiles_AreWeapons_ButNotPrimaryWeapons()
    {
        var missile = Assert.IsType<Weapon>(Equipment.Create("Light Missile"));
        Assert.Equal(EquipmentType.FixedMissile, missile.EquipmentType);
        Assert.False(missile.IsPrimary);
    }

    [Fact]
    public void AWeapon_CanOnlyBeCreatedWithAWeaponKind()
    {
        Assert.Throws<System.ArgumentException>(() => new Weapon(EquipmentType.Shield));
        Assert.Throws<System.ArgumentException>(() => new Weapon(EquipmentType.Unkown));
    }

    [Fact]
    public void EveryWeapon_HasARangeAndDamage_AndItsLookMatchesItsImpactTime()
    {
        foreach (var name in WeaponNames)
        {
            var weapon = (Weapon)Equipment.Create(name);
            Assert.True(weapon.Range > 0f, name);
            Assert.True(weapon.Damage > 0, name);
            Assert.Equal(ProjectileVisuals.For(name).BurstDuration, weapon.ImpactLingerSeconds); // the burst plays for as long as the projectile lingers
        }
    }

    [Fact]
    public void EachWeapon_HasItsOwnLook_AndUnknownWeaponsUseTheDefault()
    {
        Assert.Equal(new Color(255, 245, 200), ProjectileVisuals.For("Light Laser").CoreColor);
        Assert.Equal(ProjectileVisualStyle.Beam, ProjectileVisuals.For("Plasma Beam").Style);
        Assert.Equal(ProjectileVisualStyle.Pulse, ProjectileVisuals.For("Pulse Cannon").Style);

        // Spot checks of values that were once mistyped when the looks were moved into code.
        Assert.Equal(14, ProjectileVisuals.For("Mining Beam").BurstParticleCount);
        Assert.Equal(90f, ProjectileVisuals.For("Mining Beam").BurstParticleSpeed);

        Assert.Equal(ProjectileVisual.Default.CoreColor, ProjectileVisuals.For("Nothing").CoreColor);
        Assert.Equal(ProjectileVisual.Default.CoreColor, ProjectileVisuals.For(null).CoreColor);
    }

    [Fact]
    public void EnergyCost_BelongsToTheKindsThatUseIt()
    {
        Assert.Null(typeof(Equipment).GetProperty("EnergyCost"));
        Assert.Null(typeof(HullPlating).GetProperty("EnergyCost"));
        Assert.Null(typeof(Engine).GetProperty("EnergyCost"));
        Assert.NotNull(typeof(Weapon).GetProperty("EnergyCost"));
        Assert.NotNull(typeof(Shield).GetProperty("EnergyCost"));
        Assert.NotNull(typeof(UtilityModule).GetProperty("EnergyCost"));

        // The values did not change when the property moved.
        Assert.Equal(10f, ((Weapon)Equipment.Create("Light Laser")).EnergyCost);
        Assert.Equal(40f, ((Weapon)Equipment.Create("Plasma Beam")).EnergyCost);
        Assert.Equal(20f, ((Shield)Equipment.Create("Basic Shield Generator")).EnergyCost);
        Assert.Equal(70f, ((Shield)Equipment.Create("Military Shield Generator")).EnergyCost);
        Assert.Equal(5f, ((UtilityModule)Equipment.Create("Sensor Array")).EnergyCost);
        Assert.Equal(0f, ((UtilityModule)Equipment.Create("Cargo Expander")).EnergyCost); // never had one
    }
}
