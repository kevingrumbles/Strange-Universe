using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Strange_Universe.Game.Components;
using Strange_Universe.Game.Systems;
using Xunit;

namespace Strange_Universe.Tests;

/// <summary>Round 2, Phase 7.2: weapons and projectile styles come from JSON.</summary>
public class DefinitionTests
{
    private const string TestWeaponJson = """
    {
      "weapons": [
        { "name": "Test Blaster", "type": "TurretProjectile", "damage": 99, "speed": 700, "range": 400,
          "fireRate": 2, "energyCost": 1, "mass": 5, "accuracy": 1, "impactLingerSeconds": 0.5, "visualStyle": "testGreen" }
      ],
      "visualStyles": {
        "default":   { "style": "laser", "coreColor": [1, 2, 3], "glowColor": [4, 5, 6], "burstDuration": 0.18 },
        "testGreen": { "style": "pulse", "coreColor": [0, 255, 0], "glowColor": [0, 100, 0], "coreLength": 9, "burstDuration": 0.5 }
      }
    }
    """;

    private static DefinitionRepository FromJson(string json) =>
        DefinitionRepository.Load(new MemoryStream(Encoding.UTF8.GetBytes(json)));

    [Fact]
    public void NewWeapon_NeedsNoCodeChange()
    {
        var repo = FromJson(TestWeaponJson);

        var gun = repo.CreateWeapons().Single();
        Assert.Equal("Test Blaster", gun.EquipmentName);
        Assert.Equal(EquipmentType.TurretProjectile, gun.EquipmentType);
        Assert.Equal(99, gun.Damage);
        Assert.Equal(0.5f, gun.ImpactLingerSeconds);
        Assert.True(gun.PrimaryWeapon);

        var visual = ProjectileVisuals.For(repo, "Test Blaster");
        Assert.Equal(new Color(0, 255, 0), visual.CoreColor);
        Assert.Equal(ProjectileVisualStyle.Pulse, visual.Style);
        Assert.Equal(9, visual.CoreLength);
        Assert.Equal(gun.ImpactLingerSeconds, visual.BurstDuration);
    }

    [Fact]
    public void UnknownWeapon_UsesDefaultStyle()
    {
        var repo = FromJson(TestWeaponJson);
        Assert.Equal(new Color(1, 2, 3), ProjectileVisuals.For(repo, "Nothing").CoreColor);
        Assert.Equal(new Color(1, 2, 3), ProjectileVisuals.For(repo, null).CoreColor);
    }

    [Fact]
    public void Stock_Definitions_LoadAndPopulatePresets()
    {
        var repo = DefinitionRepository.Default;
        Assert.Equal(6, repo.Weapons.Count);

        var light = Equipment.FromName("light laser");
        Assert.Equal(15, light.Damage);
        Assert.Equal(EquipmentType.FixedProjectile, light.EquipmentType);
        Assert.Equal(0.16f, light.ImpactLingerSeconds);
        Assert.Equal(new Color(255, 245, 200), ProjectileVisuals.For("Light Laser").CoreColor);
        Assert.Equal(ProjectileVisualStyle.Beam, ProjectileVisuals.For("Plasma Beam").Style);

        // Non-weapon equipment is still defined in code.
        Assert.Equal(EquipmentType.Shield, Equipment.FromName("Basic Shield Generator").EquipmentType);
    }
}
