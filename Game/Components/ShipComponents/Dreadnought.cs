using Strange_Universe.Game.Helpers;
using Strange_Universe.Game.Components.EquipmentComponents;

namespace Strange_Universe.Game.Components.ShipComponents;

/// <summary>The Dreadnought: a vast armoured gun platform with the heaviest weapons, shields and armour, and the slowest turn of any ship.</summary>
public sealed class Dreadnought : ShipStats
{
    public override string ShipTypeName => "Dreadnought";

    public Dreadnought()
    {
        Flight.ThrustForce   = 560f;
        Flight.RotationSpeed = 1.6f;
        Flight.MaxSpeed      = 480f;
        Flight.LinearDamping = 1f;
        Flight.SoftCapStart  = 0.75f;
        Flight.Radius        = 30f;

        Durability.MaxHull = 20;
        Durability.MaxFuel = 10;

        Appearance.SplashName    = "Art/Shuttle_splash.png";
        Appearance.SpriteScale   = 0.85f;
        Appearance.HullSeed      = 67;
        Appearance.BodyWidth     = 0.36f;
        Appearance.NoseSharpness = 0.45f;
        Appearance.Blockiness    = 0.9f;
        Appearance.BridgeForward = 1f;
        Appearance.WingPairs     = 1;
        Appearance.WingSpan      = 0.38f;
        Appearance.WingSweep     = 0.1f;
        Appearance.WingChord     = 0.3f;
        Appearance.EngineCount   = 4;
        Appearance.WindowCount   = 8;
        Appearance.HullColor     = new Rgba(66, 70, 82);
        Appearance.AccentColor   = new Rgba(112, 26, 30);

        DefaultLoadout = new EquipmentComponent[] { EquipmentCatalog.PlasmaGun, EquipmentCatalog.PlasmaBeam, EquipmentCatalog.HeavyTorpedo, EquipmentCatalog.HeavyLaser, EquipmentCatalog.MilitaryShieldGenerator, EquipmentCatalog.MilitaryArmor };
        TypicalCredits = 1500;
        Price = 14000;
        Grade = 4;

        Limits.MaxWeapons = 5;
        Limits.MaxShields = 2;
        Limits.MaxPlating = 2;
        Limits.MaxEngines = 1;
        Limits.MaxUtilities = 2;
        Limits.MaxMass = 2600f;
    }
}
