using Strange_Universe.Game.Helpers;
using Strange_Universe.Game.Components.EquipmentComponents;

namespace Strange_Universe.Game.Components.ShipComponents;

/// <summary>The Hauler: a slow, boxy cargo ship with room for extra cargo expanders and little to defend itself with.</summary>
public sealed class Hauler : ShipStats
{
    public override string ShipTypeName => "Hauler";

    public Hauler()
    {
        Flight.ThrustForce   = 300f;
        Flight.RotationSpeed = 2.5f;
        Flight.MaxSpeed      = 480f;
        Flight.LinearDamping = 1f;
        Flight.SoftCapStart  = 0.75f;
        Flight.Radius        = 20f;

        Durability.MaxHull = 8;
        Durability.MaxFuel = 10;

        Appearance.SplashName    = "Art/Shuttle_splash.png";
        Appearance.SpriteScale   = 0.85f;
        Appearance.HullSeed      = 31;
        Appearance.BodyWidth     = 0.34f;
        Appearance.NoseSharpness = 0.15f;
        Appearance.Blockiness    = 1f;
        Appearance.WingPairs     = 0;
        Appearance.WingSpan      = 0.4f;
        Appearance.WingSweep     = 0.04f;
        Appearance.WingChord     = 0.3f;
        Appearance.EngineCount   = 2;
        Appearance.WindowCount   = 4;
        Appearance.HullColor     = new Rgba(84, 80, 70);
        Appearance.AccentColor   = new Rgba(150, 108, 40);

        DefaultLoadout = new EquipmentComponent[] { EquipmentCatalog.LightLaser, EquipmentCatalog.BasicShieldGenerator, EquipmentCatalog.LightHullPlating, EquipmentCatalog.CargoExpander, EquipmentCatalog.CargoExpander };
        TypicalCredits = 300;
        Price = 1800;
        Grade = 1;

        Limits.MaxWeapons = 1;
        Limits.MaxShields = 1;
        Limits.MaxPlating = 1;
        Limits.MaxEngines = 1;
        Limits.MaxUtilities = 4;
        Limits.MaxMass = 1000f;
    }
}
