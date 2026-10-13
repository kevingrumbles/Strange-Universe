using System;
using System.Collections.Generic;
using System.Linq;

namespace Strange_Universe.Game.Components;

/// <summary>
/// Every built-in item of equipment, defined in code. The items in <see cref="All"/> are shared instances (a weapon has a
/// cooldown), so this is internal: ships get their own copies from <see cref="Equipment.Create"/>.
/// </summary>
internal static class EquipmentCatalog
{
    // -- Weapons -----------------------------------------------------------------

    public static Weapon LightLaser => new(EquipmentType.FixedProjectile)
    {
        EquipmentName       = "Light Laser",
        Damage              = 15,
        ProjectileSpeed     = 2000f,
        Range               = 1500f,
        FireRate            = 3f,
        EnergyCost          = 10f,
        Mass                = 50f,
        Accuracy            = 0.9f,
        ImpactLingerSeconds = 0.16f,
    };

    public static Weapon HeavyLaser => new(EquipmentType.FixedProjectile)
    {
        EquipmentName       = "Heavy Laser",
        Damage              = 35,
        ProjectileSpeed     = 1800f,
        Range               = 2000f,
        FireRate            = 1.5f,
        EnergyCost          = 25f,
        Mass                = 120f,
        Accuracy            = 0.85f,
        ImpactLingerSeconds = 0.26f,
    };

    public static Weapon PulseCannon => new(EquipmentType.TurretProjectile)
    {
        EquipmentName       = "Pulse Cannon",
        Damage              = 20,
        ProjectileSpeed     = 1500f,
        Range               = 1200f,
        FireRate            = 4f,
        EnergyCost          = 12f,
        Mass                = 80f,
        Accuracy            = 0.9f,
        ImpactLingerSeconds = 0.22f,
    };

    public static Weapon GatlingGun => new(EquipmentType.FixedProjectile)
    {
        EquipmentName       = "Gatling Gun",
        Damage              = 8,
        ProjectileSpeed     = 2500f,
        Range               = 1000f,
        FireRate            = 10f,
        EnergyCost          = 5f,
        Mass                = 60f,
        Accuracy            = 0.95f,
        ImpactLingerSeconds = 0.10f,
    };

    public static Weapon PlasmaBeam => new(EquipmentType.FixedBeam)
    {
        EquipmentName       = "Plasma Beam",
        Damage              = 50,
        Range               = 2500f,
        EnergyCost          = 40f,
        Mass                = 150f,
        Accuracy            = 0.8f,
        ImpactLingerSeconds = 0.34f,
    };

    public static Weapon MiningBeam => new(EquipmentType.TurretBeam)
    {
        EquipmentName       = "Mining Beam",
        Damage              = 5,
        Range               = 800f,
        EnergyCost          = 15f,
        Mass                = 90f,
        Accuracy            = 0.95f,
        ImpactLingerSeconds = 0.20f,
    };

    public static Weapon LightMissile => new(EquipmentType.FixedMissile)
    {
        EquipmentName   = "Light Missile",
        Damage          = 80,
        ProjectileSpeed = 500f,
        Range           = 3000f,
        FireRate        = 0.5f,
        EnergyCost      = 30f,
        Mass            = 100f,
    };

    public static Weapon HeavyTorpedo => new(EquipmentType.TurretMissile)
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

    public static Shield BasicShieldGenerator => new()
    {
        EquipmentName = "Basic Shield Generator",
        ShieldStrength = 500f,
        RechargeRate   = 25f,
        RechargeDelay  = 5f,
        EnergyCost     = 20f,
        Mass           = 200f,
    };

    public static Shield AdvancedShieldGenerator => new()
    {
        EquipmentName = "Advanced Shield Generator",
        ShieldStrength = 1200f,
        RechargeRate   = 50f,
        RechargeDelay  = 3f,
        EnergyCost     = 40f,
        Mass           = 400f,
    };

    public static Shield MilitaryShieldGenerator => new()
    {
        EquipmentName = "Military Shield Generator",
        ShieldStrength = 2500f,
        RechargeRate   = 80f,
        RechargeDelay  = 2f,
        EnergyCost     = 70f,
        Mass           = 600f,
    };

    // -- Hull --------------------------------------------------------------------

    public static HullPlating LightHullPlating => new()
    {
        EquipmentName = "Light Hull Plating",
        HullStrength  = 300f,
        Mass          = 150f,
    };

    public static HullPlating ReinforcedHull => new()
    {
        EquipmentName = "Reinforced Hull",
        HullStrength  = 800f,
        Mass          = 400f,
    };

    public static HullPlating MilitaryArmor => new()
    {
        EquipmentName = "Military Armor",
        HullStrength  = 1500f,
        Mass          = 800f,
    };

    // -- Engines -----------------------------------------------------------------

    public static Engine StandardEngine => new()
    {
        EquipmentName = "Standard Engine",
        EngineThrust  = 1000f,
        FuelLevel     = 100f,
        Mass          = 300f,
    };

    public static Engine HighPerformanceEngine => new()
    {
        EquipmentName = "High-Performance Engine",
        EngineThrust  = 2000f,
        FuelLevel     = 80f,
        Mass          = 450f,
    };

    public static Engine EfficientEngine => new()
    {
        EquipmentName = "Efficient Engine",
        EngineThrust  = 1200f,
        FuelLevel     = 150f,
        Mass          = 350f,
    };

    // -- Utility -----------------------------------------------------------------

    public static UtilityModule CargoExpander => new()
    {
        EquipmentName = "Cargo Expander",
        Mass          = 100f,
    };

    public static UtilityModule SensorArray => new()
    {
        EquipmentName = "Sensor Array",
        EnergyCost    = 5f,
        Mass          = 75f,
    };

    public static UtilityModule AutoRepairModule => new()
    {
        EquipmentName = "Auto-Repair Module",
        EnergyCost    = 15f,
        Mass          = 150f,
    };

    /// <summary>Every built-in item (shared instances: do not hand these to ships).</summary>
    public static IReadOnlyList<Equipment> All { get; } = new Equipment[]
    {
        LightLaser, HeavyLaser, PulseCannon, GatlingGun, PlasmaBeam, MiningBeam,
        LightMissile, HeavyTorpedo,
        BasicShieldGenerator, AdvancedShieldGenerator, MilitaryShieldGenerator,
        LightHullPlating, ReinforcedHull, MilitaryArmor,
        StandardEngine, HighPerformanceEngine, EfficientEngine,
        CargoExpander, SensorArray, AutoRepairModule,
    };

    /// <summary>The shared item with this name (not case sensitive), or null.</summary>
    public static Equipment Find(string equipmentName)
        => All.FirstOrDefault(e => string.Equals(e.EquipmentName, equipmentName, StringComparison.OrdinalIgnoreCase));
}
