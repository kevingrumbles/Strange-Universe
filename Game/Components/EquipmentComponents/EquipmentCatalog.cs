using System;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Linq;

namespace Strange_Universe.Game.Components.EquipmentComponents;

/// <summary>
/// Every built-in item of equipment, defined in code. Each item is built fresh whenever it is asked for (a weapon has a
/// cooldown, which must not be shared between ships), so no two ships ever hold the same object. Use <see cref="Equipment.Create"/>.
/// </summary>
internal static class EquipmentCatalog
{
    // -- Weapons -----------------------------------------------------------------

    public static WeaponComponent LightLaser => new(EquipmentType.FixedProjectile)
    {
        EquipmentName       = "Light Laser",
        Damage              = 15,
        ProjectileSpeed     = 2000f,
        Range               = 1500f,
        FireRate            = 3f,
        EnergyCost          = 10f,
        Mass                = 50f,
        Accuracy            = 1.0f,
        Visual = new ProjectileVisual
        {
            BurstDuration = 0.16f, BurstRadius = 16f,
        },
    };

    public static WeaponComponent HeavyLaser => new(EquipmentType.FixedProjectile)
    {
        EquipmentName       = "Heavy Laser",
        Damage              = 35,
        ProjectileSpeed     = 1800f,
        Range               = 2000f,
        FireRate            = 1.5f,
        EnergyCost          = 25f,
        Mass                = 120f,
        Accuracy            = 0.85f,
        Visual = new ProjectileVisual
        {
            CoreLength = 30, CoreWidth = 5,
            GlowRadius = 14, GlowIntensity = 0.9f,
            TrailLength = 40, TrailWidth = 8, TrailAlpha = 0.5f,
            PulseSpeed = 6f, PulseAmount = 0.22f,
            ParticleCount = 16,
            BurstDuration = 0.26f, BurstRadius = 30f, BurstParticleCount = 20, BurstParticleSpeed = 190f,
        },
    };

    public static WeaponComponent PulseCannon => new(EquipmentType.TurretProjectile)
    {
        EquipmentName       = "Pulse Cannon",
        Damage              = 20,
        ProjectileSpeed     = 1500f,
        Range               = 1200f,
        FireRate            = 4f,
        EnergyCost          = 12f,
        Mass                = 80f,
        Accuracy            = 0.9f,
        Visual = new ProjectileVisual
        {
            CoreColor = new Color(230, 255, 230), CoreLength = 14, CoreWidth = 6,
            GlowColor = new Color(90, 255, 130), GlowRadius = 12, GlowIntensity = 0.85f,
            TrailLength = 20, TrailWidth = 6, TrailAlpha = 0.4f,
            PulseSpeed = 12f, PulseAmount = 0.3f,
            ParticleCount = 10,
            BurstDuration = 0.22f, BurstRadius = 24f, BurstParticleCount = 16, BurstParticleSpeed = 170f,
        },
    };

    public static WeaponComponent GatlingGun => new(EquipmentType.FixedProjectile)
    {
        EquipmentName       = "Gatling Gun",
        Damage              = 8,
        ProjectileSpeed     = 2500f,
        Range               = 1000f,
        FireRate            = 10f,
        EnergyCost          = 5f,
        Mass                = 60f,
        Accuracy            = 0.95f,
        Visual = new ProjectileVisual
        {
            CoreColor = new Color(255, 250, 220), CoreLength = 10, CoreWidth = 2,
            GlowColor = new Color(255, 170, 60), GlowRadius = 4, GlowIntensity = 0.5f,
            TrailLength = 14, TrailWidth = 2, TrailAlpha = 0.25f,
            PulseSpeed = 0f, PulseAmount = 0f,
            ParticleCount = 4,
            BurstDuration = 0.10f, BurstRadius = 8f, BurstParticleCount = 6, BurstParticleSpeed = 150f,
        },
    };

    public static WeaponComponent PlasmaGun => new(EquipmentType.FixedProjectile)
    {
        EquipmentName = "Plasma Gun",
        Damage = 35,
        ProjectileSpeed = 1800f,
        Range = 2000f,
        FireRate = 1.5f,
        EnergyCost = 25f,
        Mass = 120f,
        Accuracy = 0.85f,
        Visual = new ProjectileVisual 
        {
            CoreLength = 30,
            CoreWidth = 5,
            GlowRadius = 14,
            GlowIntensity = 0.9f,
            TrailLength = 40,
            TrailWidth = 8,
            TrailAlpha = 0.5f,
            PulseSpeed = 6f,
            PulseAmount = 0.22f,
            ParticleCount = 16,
            BurstDuration = 0.26f,
            BurstRadius = 30f,
            BurstParticleCount = 20,
            BurstParticleSpeed = 190f,
            CoreColor = new Color(245, 225, 255),
            GlowColor = new Color(180, 80, 255)
        },
    };

    public static WeaponComponent PlasmaBeam => new(EquipmentType.FixedBeam)
    {
        EquipmentName       = "Plasma Beam",
        Damage              = 50,
        Range               = 2500f,
        EnergyCost          = 40f,
        Mass                = 150f,
        Accuracy            = 0.8f,
        Visual = new ProjectileVisual
        {
            CoreColor = new Color(245, 225, 255), CoreLength = 64, CoreWidth = 6,
            GlowColor = new Color(180, 80, 255), GlowRadius = 18, GlowIntensity = 1f,
            TrailLength = 0, TrailWidth = 0, TrailAlpha = 0f,
            PulseSpeed = 14f, PulseAmount = 0.25f,
            ParticleCount = 20,
            BurstDuration = 0.34f, BurstRadius = 38f, BurstParticleCount = 26, BurstParticleSpeed = 160f,
        },
    };

    public static WeaponComponent MiningBeam => new(EquipmentType.TurretBeam)
    {
        EquipmentName       = "Mining Beam",
        Damage              = 5,
        Range               = 800f,
        EnergyCost          = 15f,
        Mass                = 90f,
        Accuracy            = 0.95f,
        Visual = new ProjectileVisual
        {
            CoreColor = new Color(255, 245, 215), CoreLength = 48, CoreWidth = 3,
            GlowColor = new Color(255, 160, 40), GlowRadius = 10, GlowIntensity = 0.75f,
            TrailLength = 0, TrailWidth = 0, TrailAlpha = 0f,
            PulseSpeed = 5f, PulseAmount = 0.15f,
            ParticleCount = 14,
            BurstDuration = 0.20f, BurstRadius = 14f, BurstParticleCount = 14, BurstParticleSpeed = 90f,
        },
    };

    public static WeaponComponent LightMissile => new(EquipmentType.FixedMissile)
    {
        EquipmentName   = "Light Missile",
        Damage          = 80,
        ProjectileSpeed = 500f,
        Range           = 3000f,
        FireRate        = 0.5f,
        EnergyCost      = 30f,
        Mass            = 100f,
    };

    public static WeaponComponent HeavyTorpedo => new(EquipmentType.TurretMissile)
    {
        EquipmentName   = "Heavy Torpedo",
        Damage          = 200,
        ProjectileSpeed = 300f,
        Range           = 5000f,
        FireRate        = 0.25f,
        EnergyCost      = 60f,
        Mass            = 250f,
    };

    // -- Shields -----------------------------------------------------------------

    public static ShieldComponent BasicShieldGenerator => new()
    {
        EquipmentName = "Basic Shield Generator",
        ShieldStrength = 25f,
        RechargeRate   = 5f,
        RechargeDelay  = 5f,
        EnergyCost     = 20f,
        Mass           = 200f,
    };

    public static ShieldComponent AdvancedShieldGenerator => new()
    {
        EquipmentName = "Advanced Shield Generator",
        ShieldStrength = 60f,
        RechargeRate   = 10f,
        RechargeDelay  = 3f,
        EnergyCost     = 40f,
        Mass           = 400f,
    };

    public static ShieldComponent MilitaryShieldGenerator => new()
    {
        EquipmentName = "Military Shield Generator",
        ShieldStrength = 125f,
        RechargeRate   = 16f,
        RechargeDelay  = 2f,
        EnergyCost     = 70f,
        Mass           = 600f,
    };

    // -- Hull --------------------------------------------------------------------

    public static PlatingComponent LightHullPlating => new()
    {
        EquipmentName = "Light Hull Plating",
        HullStrength  = 25f,
        Mass          = 150f,
    };

    public static PlatingComponent ReinforcedHull => new()
    {
        EquipmentName = "Reinforced Hull",
        HullStrength  = 65f,
        Mass          = 400f,
    };

    public static PlatingComponent MilitaryArmor => new()
    {
        EquipmentName = "Military Armor",
        HullStrength  = 125f,
        Mass          = 800f,
    };

    // -- Engines -----------------------------------------------------------------

    public static EngineComponent StandardEngine => new()
    {
        EquipmentName = "Standard Engine",
        EngineThrust  = 1000f,
        FuelLevel     = 100f,
        Mass          = 300f,
    };

    public static EngineComponent HighPerformanceEngine => new()
    {
        EquipmentName = "High-Performance Engine",
        EngineThrust  = 2000f,
        FuelLevel     = 80f,
        Mass          = 450f,
    };

    public static EngineComponent EfficientEngine => new()
    {
        EquipmentName = "Efficient Engine",
        EngineThrust  = 1200f,
        FuelLevel     = 150f,
        Mass          = 350f,
    };

    // -- Utility -----------------------------------------------------------------

    public static UtilityComponent CargoExpander => new()
    {
        EquipmentName = "Cargo Expander",
        Mass          = 100f,
    };

    public static UtilityComponent SensorArray => new()
    {
        EquipmentName = "Sensor Array",
        EnergyCost    = 5f,
        Mass          = 75f,
    };

    public static UtilityComponent AutoRepairModule => new()
    {
        EquipmentName = "Auto-Repair Module",
        EnergyCost    = 15f,
        Mass          = 150f,
    };

    /// <summary>How to build each built-in item. Every call builds a new object, so nothing is ever shared.</summary>
    private static readonly Func<EquipmentComponent>[] Builders =
    {
        () => LightLaser, () => HeavyLaser, () => PulseCannon, () => GatlingGun, () => PlasmaBeam, () => MiningBeam,
        () => LightMissile, () => HeavyTorpedo,
        () => BasicShieldGenerator, () => AdvancedShieldGenerator, () => MilitaryShieldGenerator,
        () => LightHullPlating, () => ReinforcedHull, () => MilitaryArmor,
        () => StandardEngine, () => HighPerformanceEngine, () => EfficientEngine,
        () => CargoExpander, () => SensorArray, () => AutoRepairModule,
    };

    private static readonly Dictionary<string, Func<EquipmentComponent>> ByName =
        Builders.ToDictionary(build => build().EquipmentName, build => build, StringComparer.OrdinalIgnoreCase);

    /// <summary>A new object for every built-in item, in catalog order.</summary>
    public static IReadOnlyList<EquipmentComponent> All => Builders.Select(build => build()).ToList();

    /// <summary>A new object for the item with this name (not case sensitive), or null if there is no such item.</summary>
    public static EquipmentComponent Create(string equipmentName)
        => equipmentName != null && ByName.TryGetValue(equipmentName, out var build) ? build() : null;
}
