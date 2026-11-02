using Strange_Universe.Game.Helpers;
using Strange_Universe.Game.Components.EquipmentComponents;

namespace Strange_Universe.Game.Components.ShipComponents;

/// <summary>The Runner: faster and better armed than the Shuttle, a slim dart with tiny swept fins.</summary>
public sealed class Runner : ShipStats
{
    public override string ShipTypeName => "Runner";

    public Runner()
    {
        Flight.ThrustForce   = 560f;
        Flight.RotationSpeed = 4.5f;
        Flight.MaxSpeed      = 780f;
        Flight.LinearDamping = 1f;
        Flight.SoftCapStart  = 0.75f;
        Flight.Radius        = 16f;

        Durability.MaxHull = 5;
        Durability.MaxFuel = 7;

        Appearance.SplashName   = "Art/Shuttle_splash.png";
        Appearance.SpriteScale  = 0.9f;
        Appearance.HullSeed     = 23;
        Appearance.BodyWidth    = 0.12f;
        Appearance.NoseSharpness = 1f;
        Appearance.Blockiness   = 0f;
        Appearance.WingPairs    = 1;
        Appearance.WingSpan     = 0.28f;
        Appearance.WingSweep    = 0.8f;
        Appearance.WingChord    = 0.38f;
        Appearance.EngineCount  = 1;
        Appearance.WindowCount  = 0;
        Appearance.HullColor    = new Rgba(78, 86, 102);
        Appearance.AccentColor  = new Rgba(140, 36, 40);

        DefaultLoadout = new EquipmentComponent[] {EquipmentCatalog.PlasmaGun, EquipmentCatalog.HeavyLaser, EquipmentCatalog.BasicShieldGenerator, EquipmentCatalog.ReinforcedHull };
        TypicalCredits = 250;
        Price = 2500;
        Grade = 2;

        Limits.MaxWeapons = 2;
        Limits.MaxShields = 1;
        Limits.MaxPlating = 1;
        Limits.MaxEngines = 1;
        Limits.MaxUtilities = 2;
        Limits.MaxMass = 900f;

    }
}
