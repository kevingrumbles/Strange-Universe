using System.Collections.Generic;
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
    Utility
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
    public float? Mass { get; set; } = null;
    public float? ShieldStrength { get; set; } = null;
    public float? HullStrength { get; set; } = null;
    public float? ShieldRechargeRate { get; set; } = null;
    public float? ShieldRechargeDelay { get; set; } = null;
    public float? EngineThrust { get; set; } = null;
    public float? FuelLevel { get; set; } = null;


    public EquipmentStats GetEquipmentStats(string name)
    {
        return Presets.Find(s => s.EquipmentName == name) ?? throw new KeyNotFoundException($"EquipmentStats preset '{name}' not found.");
    }
}
