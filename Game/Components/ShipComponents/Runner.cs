using Strange_Universe.Game.Helpers;
using Strange_Universe.Game.Components.EquipmentComponents;

namespace Strange_Universe.Game.Components.ShipComponents;

/// <summary>The Runner: faster and better armed than the Shuttle, with two Heavy Lasers.</summary>
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
        Appearance.SpriteScale  = 1.5f;
        Appearance.HullSeed     = 23;
        Appearance.BodyWidth    = 0.17f;
        Appearance.NoseSharpness = 0.9f;
        Appearance.WingPairs    = 2;
        Appearance.WingSpan     = 0.85f;
        Appearance.WingSweep    = 0.5f;
        Appearance.WingChord    = 0.45f;
        Appearance.EngineCount  = 3;
        Appearance.WindowCount  = 4;
        Appearance.HullColor    = new Rgba(120, 126, 140);
        Appearance.AccentColor  = new Rgba(210, 80, 40);

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
