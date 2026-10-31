using Strange_Universe.Game.Helpers;
using Strange_Universe.Game.Components.EquipmentComponents;

namespace Strange_Universe.Game.Components.ShipComponents;

/// <summary>The Shuttle: a slow, light ship that starts with a Light Laser, a Basic Shield Generator and Light Hull Plating.</summary>
public sealed class Shuttle : ShipStats
{
    public override string ShipTypeName => "Shuttle";

    public Shuttle()
    {
        Flight.ThrustForce   = 360f;
        Flight.RotationSpeed = 3.5f;
        Flight.MaxSpeed      = 580f;
        Flight.LinearDamping = 1f;
        Flight.SoftCapStart  = 0.75f;
        Flight.Radius        = 14f;

        Durability.MaxHull = 5;
        Durability.MaxFuel = 5;

        Appearance.SplashName   = "Art/Shuttle_splash.png";
        Appearance.SpriteScale  = 0.85f;
        Appearance.HullSeed     = 11;
        Appearance.BodyWidth    = 0.3f;
        Appearance.NoseSharpness = 0.3f;
        Appearance.Blockiness   = 0.9f;
        Appearance.WingPairs    = 1;
        Appearance.WingSpan     = 0.4f;
        Appearance.WingSweep    = 0.04f;
        Appearance.WingChord    = 0.3f;
        Appearance.EngineCount  = 2;
        Appearance.WindowCount  = 10;
        Appearance.HullColor    = new Rgba(92, 90, 86);
        Appearance.AccentColor  = new Rgba(138, 92, 34);

        DefaultLoadout = new EquipmentComponent[] { EquipmentCatalog.LightLaser, EquipmentCatalog.BasicShieldGenerator, EquipmentCatalog.LightHullPlating };
        TypicalCredits = 100;
        Price = 800;
        Grade = 1;

        Limits.MaxWeapons = 1;
        Limits.MaxShields = 1;
        Limits.MaxPlating = 1;
        Limits.MaxEngines = 1;
        Limits.MaxUtilities = 1;
        Limits.MaxMass = 450f;

    }
}
