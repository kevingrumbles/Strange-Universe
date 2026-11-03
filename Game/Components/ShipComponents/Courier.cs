using Strange_Universe.Game.Helpers;
using Strange_Universe.Game.Components.EquipmentComponents;

namespace Strange_Universe.Game.Components.ShipComponents;

/// <summary>The Courier: a small, quick and thrifty ship for carrying valuable cargo a long way, light on weapons and armour.</summary>
public sealed class Courier : ShipStats
{
    public override string ShipTypeName => "Courier";

    public Courier()
    {
        Flight.ThrustForce   = 520f;
        Flight.RotationSpeed = 4.2f;
        Flight.MaxSpeed      = 700f;
        Flight.LinearDamping = 1f;
        Flight.SoftCapStart  = 0.75f;
        Flight.Radius        = 12f;

        Durability.MaxHull   = 4;
        Durability.MaxFuel   = 12;

        Appearance.SplashName    = "Art/Courier_splash.png";
        Appearance.SpriteScale   = 0.9f;
        Appearance.HullSeed      = 41;
        Appearance.BodyWidth     = 0.13f;
        Appearance.SidePods      = 0.1f;
        Appearance.NoseSharpness = 0.7f;
        Appearance.Blockiness    = 0.25f;
        Appearance.WingPairs     = 0;
        Appearance.WingSpan      = 0.26f;
        Appearance.WingSweep     = 0.15f;
        Appearance.WingChord     = 0.25f;
        Appearance.EngineCount   = 2;
        Appearance.WindowCount   = 2;
        Appearance.HullColor     = new Rgba(88, 98, 104);
        Appearance.AccentColor   = new Rgba(56, 112, 118);

        DefaultLoadout = new EquipmentComponent[] { EquipmentCatalog.LightLaser, EquipmentCatalog.BasicShieldGenerator, EquipmentCatalog.EfficientEngine, EquipmentCatalog.CargoExpander };
        TypicalCredits = 400;
        Price = 2200;
        Grade = 2;

        Limits.MaxWeapons = 1;
        Limits.MaxShields = 1;
        Limits.MaxPlating = 1;
        Limits.MaxEngines = 1;
        Limits.MaxUtilities = 3;
        Limits.MaxMass = 800f;
    }
}
