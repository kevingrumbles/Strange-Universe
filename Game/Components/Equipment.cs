using Strange_Universe.Game.Entities;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Strange_Universe.Game.Components;
public enum EquipmentType
{
    FixedProjectile,
    TurretProjectile,
    FixedBeam,
    TurretBeam,
    FixedMissile,
    TurretMissile,
    Shield,
    Hull,
    Engine,
    Utility,
    Unkown
}

public class Equipment
{
    // -- Weapons -----------------------------------------------------------------

    public static Equipment LightLaser => new()
    {
        EquipmentName    = "Light Laser",
        EquipmentType    = EquipmentType.FixedProjectile,
        Damage           = 15,
        Speed            = 2000f,
        Range            = 1500f,
        FireRate         = 3f,
        EnergyCost       = 10f,
        Mass             = 50f,
        Accuracy         = 0.9f,
        ImpactLingerSeconds = 0.16f,
    };

    public static Equipment HeavyLaser => new()
    {
        EquipmentName    = "Heavy Laser",
        EquipmentType    = EquipmentType.FixedProjectile,
        Damage           = 35,
        Speed            = 1800f,
        Range            = 2000f,
        FireRate         = 1.5f,
        EnergyCost       = 25f,
        Mass             = 120f,
        Accuracy         = 0.85f,
        ImpactLingerSeconds = 0.26f,
    };

    public static Equipment PulseCannon => new()
    {
        EquipmentName    = "Pulse Cannon",
        EquipmentType    = EquipmentType.TurretProjectile,
        Damage           = 20,
        Speed            = 1500f,
        Range            = 1200f,
        FireRate         = 4f,
        EnergyCost       = 12f,
        Mass             = 80f,
        Accuracy         = 0.9f,
        ImpactLingerSeconds = 0.22f,
    };

    public static Equipment GatlingGun => new()
    {
        EquipmentName    = "Gatling Gun",
        EquipmentType    = EquipmentType.FixedProjectile,
        Damage           = 8,
        Speed            = 2500f,
        Range            = 1000f,
        FireRate         = 10f,
        EnergyCost       = 5f,
        Mass             = 60f,
        Accuracy         = 0.95f,
        ImpactLingerSeconds = 0.10f,
    };

    public static Equipment PlasmaBeam => new()
    {
        EquipmentName    = "Plasma Beam",
        EquipmentType    = EquipmentType.FixedBeam,
        Damage           = 50,
        Range            = 2500f,
        EnergyCost       = 40f,
        Mass             = 150f,
        Accuracy         = 0.8f,
        ImpactLingerSeconds = 0.34f,
    };

    public static Equipment MiningBeam => new()
    {
        EquipmentName    = "Mining Beam",
        EquipmentType    = EquipmentType.TurretBeam,
        Damage           = 5,
        Range            = 800f,
        EnergyCost       = 15f,
        Mass             = 90f,
        Accuracy         = 0.95f,
        ImpactLingerSeconds = 0.20f,
    };

    public static Equipment LightMissile => new()
    {
        EquipmentName = "Light Missile",
        EquipmentType = EquipmentType.FixedMissile,
        Damage        = 80,
        Speed         = 500f,
        Range         = 3000f,
        FireRate      = 0.5f,
        EnergyCost    = 30f,
        Mass          = 100f,
    };

    public static Equipment HeavyTorpedo => new()
    {
        EquipmentName = "Heavy Torpedo",
        EquipmentType = EquipmentType.TurretMissile,
        Damage        = 200,
        Speed         = 300f,
        Range         = 5000f,
        FireRate      = 0.25f,
        EnergyCost    = 60f,
        Mass          = 250f,
    };

    // -- Shields -----------------------------------------------------------------

    public static Equipment BasicShieldGenerator => new()
    {
        EquipmentName       = "Basic Shield Generator",
        EquipmentType       = EquipmentType.Shield,
        ShieldStrength      = 500f,
        ShieldRechargeRate  = 25f,
        ShieldRechargeDelay = 5f,
        EnergyCost          = 20f,
        Mass                = 200f,
    };

    public static Equipment AdvancedShieldGenerator => new()
    {
        EquipmentName       = "Advanced Shield Generator",
        EquipmentType       = EquipmentType.Shield,
        ShieldStrength      = 1200f,
        ShieldRechargeRate  = 50f,
        ShieldRechargeDelay = 3f,
        EnergyCost          = 40f,
        Mass                = 400f,
    };

    public static Equipment MilitaryShieldGenerator => new()
    {
        EquipmentName       = "Military Shield Generator",
        EquipmentType       = EquipmentType.Shield,
        ShieldStrength      = 2500f,
        ShieldRechargeRate  = 80f,
        ShieldRechargeDelay = 2f,
        EnergyCost          = 70f,
        Mass                = 600f,
    };

    // -- Hull --------------------------------------------------------------------

    public static Equipment LightHullPlating => new()
    {
        EquipmentName = "Light Hull Plating",
        EquipmentType = EquipmentType.Hull,
        HullStrength  = 300f,
        Mass          = 150f,
    };

    public static Equipment ReinforcedHull => new()
    {
        EquipmentName = "Reinforced Hull",
        EquipmentType = EquipmentType.Hull,
        HullStrength  = 800f,
        Mass          = 400f,
    };

    public static Equipment MilitaryArmor => new()
    {
        EquipmentName = "Military Armor",
        EquipmentType = EquipmentType.Hull,
        HullStrength  = 1500f,
        Mass          = 800f,
    };

    // -- Engines -----------------------------------------------------------------

    public static Equipment StandardEngine => new()
    {
        EquipmentName = "Standard Engine",
        EquipmentType = EquipmentType.Engine,
        EngineThrust  = 1000f,
        FuelLevel     = 100f,
        Mass          = 300f,
    };

    public static Equipment HighPerformanceEngine => new()
    {
        EquipmentName = "High-Performance Engine",
        EquipmentType = EquipmentType.Engine,
        EngineThrust  = 2000f,
        FuelLevel     = 80f,
        Mass          = 450f,
    };

    public static Equipment EfficientEngine => new()
    {
        EquipmentName = "Efficient Engine",
        EquipmentType = EquipmentType.Engine,
        EngineThrust  = 1200f,
        FuelLevel     = 150f,
        Mass          = 350f,
    };

    // -- Utility -----------------------------------------------------------------

    public static Equipment CargoExpander => new()
    {
        EquipmentName = "Cargo Expander",
        EquipmentType = EquipmentType.Utility,
        Mass          = 100f,
    };

    public static Equipment SensorArray => new()
    {
        EquipmentName = "Sensor Array",
        EquipmentType = EquipmentType.Utility,
        EnergyCost    = 5f,
        Mass          = 75f,
    };

    public static Equipment AutoRepairModule => new()
    {
        EquipmentName = "Auto-Repair Module",
        EquipmentType = EquipmentType.Utility,
        EnergyCost    = 15f,
        Mass          = 150f,
    };

    /// <summary>All built-in equipment definitions.</summary>
    public static List<Equipment> Presets { get; } = new()
    {
        LightLaser,
        HeavyLaser,
        PulseCannon,
        GatlingGun,
        PlasmaBeam,
        MiningBeam,
        LightMissile,
        HeavyTorpedo,
        BasicShieldGenerator,
        AdvancedShieldGenerator,
        MilitaryShieldGenerator,
        LightHullPlating,
        ReinforcedHull,
        MilitaryArmor,
        StandardEngine,
        HighPerformanceEngine,
        EfficientEngine,
        CargoExpander,
        SensorArray,
        AutoRepairModule,
    };

    /// <summary>
    /// Resolves a preset by name. Only <see cref="EquipmentName"/> is persisted, so this
    /// restores the full stat set (including <see cref="ImpactLingerSeconds"/>) on load.
    /// Returns an inert <see cref="EquipmentType.Unkown"/> item if the name is unrecognized.
    /// </summary>
    public static Equipment FromName(string equipmentName)
        => Presets.Find(e => string.Equals(e.EquipmentName, equipmentName, StringComparison.OrdinalIgnoreCase))
           ?? new Equipment { EquipmentName = equipmentName, EquipmentType = EquipmentType.Unkown };

    public string EquipmentName { get; set; }
    public EquipmentType EquipmentType { get; set; }
    public int? Damage { get; set; } = null;
    public float? Speed { get; set; } = null;
    public float? Range { get; set; } = null;
    public float? FireRate { get; set; } = null;
    public float? EnergyCost { get; set; } = null;
    public float? Accuracy { get; set; } = null;
    public float? Mass { get; set; } = null;
    public float? ShieldStrength { get; set; } = null;
    public float? HullStrength { get; set; } = null;
    public float? ShieldRechargeRate { get; set; } = null;
    public float? ShieldRechargeDelay { get; set; } = null;
    public float? EngineThrust { get; set; } = null;
    public float? FuelLevel { get; set; } = null;
    public bool CanFire => _cooldown <= 0f;
    public bool PrimaryWeapon
    {
        get
        {
            switch (EquipmentType)
            {
                case EquipmentType.FixedProjectile:
                case EquipmentType.TurretProjectile:
                case EquipmentType.FixedBeam:
                case EquipmentType.TurretBeam:
                    return true;
                default: return false;
            }
        }
    }
    public float ProjectileSpeed    { get; set; } = 1000f;
    public float FiringOffset       { get; set; } = 20f;
    public float ProjectileRadius   { get; set; } = 4f;

    /// <summary>Seconds a spent projectile stays in the world after impact (gameplay; the renderer plays its burst over this time).</summary>
    [JsonIgnore] public float ImpactLingerSeconds { get; set; } = 0.18f;
    private static readonly Random _rng = new();
    private float _cooldown = 0f;

    public Equipment()
    {
        // Default constructor for JSON deserialization
    }

    /// <summary>Advances the weapon cooldown. Call every frame for weapon equipment.</summary>
    public void UpdateCooldown(float deltaTime)
    {
        if (_cooldown > 0f)
            _cooldown -= deltaTime;
    }

    /// <summary>
    /// Attempts to fire. Returns a new <see cref="Projectile"/> if the cooldown
    /// has expired, otherwise returns null.
    /// </summary>
    /// <param name="owner">The ship firing this weapon.</param>
    /// <param name="attackSpeedBonus">Additive attacks-per-second bonus from ship utility gear.</param>
    /// <param name="attackRangeBonus">Additive range multiplier bonus from ship utility gear.</param>
    /// <param name="accuracyBonus">Additive accuracy bonus from ship utility gear.</param>
    public Projectile TryFire(Ship owner, float attackSpeedBonus = 0f, float attackRangeBonus = 0f, float accuracyBonus = 0f)
    {
        if (!CanFire)
            return null;

        float effectiveRate  = (FireRate ?? 1f) + attackSpeedBonus;
        float effectiveRange = (Range    ?? 0f) + attackRangeBonus;
        float effectiveAccuracy = (Accuracy ?? 0f) + accuracyBonus;

        // A weapon with no configured range would otherwise despawn its projectile
        // on the next frame, making it invisible.
        if (effectiveRange <= 0f)
            return null;
        _cooldown = effectiveRate > 0f ? 1f / effectiveRate : 0f;

        Vector2 spawnPos = owner.Position + owner.Forward * FiringOffset;

        // Accuracy of 1 = no spread; 0 = up to ±22.5° spread
        float accuracyClamped = Math.Clamp(effectiveAccuracy, 0f, 1f);
        float maxSpread       = (1f - accuracyClamped) * (MathF.PI / 8f); // 0 → ±22.5°
        float spreadAngle     = ((float)_rng.NextDouble() * 2f - 1f) * maxSpread;

        float cos = MathF.Cos(spreadAngle);
        float sin = MathF.Sin(spreadAngle);
        Vector2 forward = owner.Forward;
        Vector2 firedDir = new Vector2(
            forward.X * cos - forward.Y * sin,
            forward.X * sin + forward.Y * cos);

        Vector2 velocity = owner.Velocity + firedDir * (Speed ?? ProjectileSpeed);

        float speed    = velocity.Length();
        float lifetime = speed > 0f ? effectiveRange / speed : 0f;

        return new Projectile
        {
            Owner      = owner,
            Position   = spawnPos,
            Velocity   = velocity,
            Rotation   = owner.Rotation + spreadAngle,
            Lifetime   = lifetime,
            Radius     = ProjectileRadius,
            ImpactLingerSeconds = ImpactLingerSeconds,
            Damage     = Damage ?? 0,
            WeaponType = EquipmentType,
            WeaponName = EquipmentName,
        };
    }
}

/// <summary>
/// Serializes <see cref="Ship.Equipment"/> as a JSON array of name strings.
/// On read each name is looked up in <see cref="EquipmentStats.Presets"/> to restore the full stat set.
/// </summary>
public class EquipmentListConverter : JsonConverter<List<Equipment>>
{
    public override List<Equipment> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var list = new List<Equipment>();
        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException("Expected start of array for Equipment.");

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            string name = reader.GetString()
                ?? throw new JsonException("Expected a non-null equipment name string.");

            list.Add(Equipment.FromName(name));
        }
        return list;
    }

    public override void Write(Utf8JsonWriter writer, List<Equipment> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var item in value)
            writer.WriteStringValue(item.EquipmentName);
        writer.WriteEndArray();
    }
}
