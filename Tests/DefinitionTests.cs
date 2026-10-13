using Microsoft.Xna.Framework;
using Strange_Universe.Game.Components;
using Strange_Universe.Game.Systems;
using Xunit;

namespace Strange_Universe.Tests;

/// <summary>Weapons are ordinary equipment presets, defined in code like shields, hulls and engines.</summary>
public class DefinitionTests
{
    private static readonly string[] WeaponNames =
        { "Light Laser", "Heavy Laser", "Pulse Cannon", "Gatling Gun", "Plasma Beam", "Mining Beam" };

    [Fact]
    public void Weapons_AreInThePresetList_LikeAnyOtherEquipment()
    {
        foreach (var name in WeaponNames)
        {
            var weapon = Equipment.FromName(name);
            Assert.Equal(name, weapon.EquipmentName);
            Assert.True(weapon.PrimaryWeapon, name);
            Assert.Contains(Equipment.Presets, e => e.EquipmentName == name);
        }

        // Other equipment is looked up the same way.
        Assert.Equal(EquipmentType.Shield, Equipment.FromName("Basic Shield Generator").EquipmentType);
        Assert.Equal(EquipmentType.Unkown, Equipment.FromName("No Such Thing").EquipmentType);
    }

    [Fact]
    public void LightLaser_HasItsStats()
    {
        var light = Equipment.FromName("light laser"); // names are not case sensitive
        Assert.Equal(EquipmentType.FixedProjectile, light.EquipmentType);
        Assert.Equal(15, light.Damage);
        Assert.Equal(2000f, light.Speed);
        Assert.Equal(1500f, light.Range);
        Assert.Equal(3f, light.FireRate);
        Assert.Equal(0.16f, light.ImpactLingerSeconds);
    }

    [Fact]
    public void EveryWeapon_HasARangeAndDamage_AndItsLookMatchesItsImpactTime()
    {
        foreach (var name in WeaponNames)
        {
            var weapon = Equipment.FromName(name);
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
}
