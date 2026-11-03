using Strange_Universe.Game.Helpers;
using Strange_Universe.Game.Components.EquipmentComponents;

namespace Strange_Universe.Game.Components.ShipComponents;

/// <summary>The Interceptor: a tiny, fast, agile fighter with two guns and very little armour.</summary>
public sealed class Interceptor : ShipStats
{
    public override string ShipTypeName => "Interceptor";

    public Interceptor()
    {
        Flight.ThrustForce   = 650f;
        Flight.RotationSpeed = 5.5f;
        Flight.MaxSpeed      = 900f;
        Flight.LinearDamping = 1f;
        Flight.SoftCapStart  = 0.75f;
        Flight.Radius        = 11f;

        Durability.MaxHull = 4;
        Durability.MaxFuel = 6;

        Appearance.SplashName    = "Art/Interceptor_splash.png";
        Appearance.SpriteScale   = 0.95f;
        Appearance.HullSeed      = 53;
        Appearance.BodyWidth     = 0.07f;
        Appearance.TwinBoom      = 0.22f;
        Appearance.NoseSharpness = 1f;
        Appearance.Blockiness    = 0f;
        Appearance.WingPairs     = 0;
        Appearance.WingSpan      = 0.28f;
        Appearance.WingSweep     = 0.35f;
        Appearance.WingChord     = 0.3f;
        Appearance.EngineCount   = 2;
        Appearance.WindowCount   = 0;
        Appearance.HullColor     = new Rgba(96, 92, 100);
        Appearance.AccentColor   = new Rgba(150, 62, 22);

        DefaultLoadout = new EquipmentComponent[] { EquipmentCatalog.GatlingGun, EquipmentCatalog.PulseCannon, EquipmentCatalog.BasicShieldGenerator, EquipmentCatalog.LightHullPlating };
        TypicalCredits = 200;
        Price = 3000;
        Grade = 2;

        Limits.MaxWeapons = 2;
        Limits.MaxShields = 1;
        Limits.MaxPlating = 1;
        Limits.MaxEngines = 1;
        Limits.MaxUtilities = 1;
        Limits.MaxMass = 600f;
    }
}
