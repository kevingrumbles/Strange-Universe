using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Strange_Universe.Game.Entities;
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
}public class Equipment
{
    public string EquipmentName { get; set; }
    [JsonIgnore] public EquipmentStats EquipmentStats 
    { 
        get
        {
            return EquipmentStats.Presets.Find(s => s.EquipmentName == EquipmentName) ?? new EquipmentStats() { EquipmentName = EquipmentName, EquipmentType = EquipmentType.Unkown };
        } 
    }
}
public class EquipmentStats
{
    public static List<EquipmentStats> Presets { get; set; } = new();
    public string EquipmentName { get; set; }
    public EquipmentType EquipmentType { get; set; }
    public float? Damage { get; set; } = null;
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

    /// <summary>Rendering style for projectiles fired by this weapon.</summary>
    [JsonIgnore] public ProjectileVisual ProjectileVisual { get; set; } = ProjectileVisual.Default;
    private static readonly Random _rng = new();
    private float _cooldown = 0f;

    public EquipmentStats()
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
        float effectiveRange = (Range    ?? 1f) + attackRangeBonus;
        float effectiveAccuracy = (Accuracy ?? 0f) + accuracyBonus;
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

        Vector2 velocity = owner.Velocity + firedDir * ProjectileSpeed;

        float speed    = velocity.Length();
        float lifetime = speed > 0f ? effectiveRange / speed : 0f;

        return new Projectile
        {
            Owner    = owner,
            Position = spawnPos,
            Velocity = velocity,
            Rotation = owner.Rotation + spreadAngle,
            Lifetime = lifetime,
            Radius   = ProjectileRadius,
            Visual   = ProjectileVisual,
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

            list.Add(new Equipment { EquipmentName = name });
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
