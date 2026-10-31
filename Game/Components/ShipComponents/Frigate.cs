using Strange_Universe.Game.Helpers;
using Strange_Universe.Game.Components.EquipmentComponents;

namespace Strange_Universe.Game.Components.ShipComponents;

/// <summary>The Frigate: a sturdy warship with three weapons mounts, a strong shield and heavy plating.</summary>
public sealed class Frigate : ShipStats
{
    public override string ShipTypeName => "Frigate";

    public Frigate()
    {
        Flight.ThrustForce   = 480f;
        Flight.RotationSpeed = 3f;
        Flight.MaxSpeed      = 600f;
        Flight.LinearDamping = 1f;
        Flight.SoftCapStart  = 0.75f;
        Flight.Radius        = 22f;

        Durability.MaxHull = 10;
        Durability.MaxFuel = 8;

        Appearance.SplashName    = "Art/Shuttle_splash.png";
        Appearance.SpriteScale   = 0.85f;
        Appearance.HullSeed      = 59;
        Appearance.BodyWidth     = 0.26f;
        Appearance.NoseSharpness = 0.5f;
        Appearance.Blockiness    = 0.6f;
        Appearance.WingPairs     = 1;
        Appearance.WingSpan      = 0.42f;
        Appearance.WingSweep     = 0.12f;
        Appearance.WingChord     = 0.35f;
        Appearance.EngineCount   = 3;
        Appearance.WindowCount   = 5;
        Appearance.HullColor     = new Rgba(70, 76, 92);
        Appearance.AccentColor   = new Rgba(130, 30, 32);

        DefaultLoadout = new EquipmentComponent[] { EquipmentCatalog.HeavyLaser, EquipmentCatalog.HeavyLaser, EquipmentCatalog.LightMissile, EquipmentCatalog.AdvancedShieldGenerator, EquipmentCatalog.ReinforcedHull };
        TypicalCredits = 500;
        Price = 5000;
        Grade = 3;

        Limits.MaxWeapons = 3;
        Limits.MaxShields = 1;
        Limits.MaxPlating = 1;
        Limits.MaxEngines = 1;
        Limits.MaxUtilities = 2;
        Limits.MaxMass = 1400f;
    }
}
