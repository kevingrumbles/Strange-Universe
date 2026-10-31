using Strange_Universe.Game.Helpers;
using Strange_Universe.Game.Components.EquipmentComponents;

namespace Strange_Universe.Game.Components.ShipComponents;

/// <summary>The Freighter: a huge, slow bulk carrier. Efficient engines, space for a great deal of cargo and thick plating, but it hardly turns.</summary>
public sealed class Freighter : ShipStats
{
    public override string ShipTypeName => "Freighter";

    public Freighter()
    {
        Flight.ThrustForce   = 340f;
        Flight.RotationSpeed = 1.8f;
        Flight.MaxSpeed      = 420f;
        Flight.LinearDamping = 1f;
        Flight.SoftCapStart  = 0.75f;
        Flight.Radius        = 27f;

        Durability.MaxHull = 14;
        Durability.MaxFuel = 16;

        Appearance.SplashName    = "Art/Shuttle_splash.png";
        Appearance.SpriteScale   = 0.85f;
        Appearance.HullSeed      = 37;
        Appearance.BodyWidth     = 0.38f;
        Appearance.NoseSharpness = 0.1f;
        Appearance.Blockiness    = 1f;
        Appearance.WingPairs     = 0;
        Appearance.WingSpan      = 0.4f;
        Appearance.WingSweep     = 0.04f;
        Appearance.WingChord     = 0.3f;
        Appearance.EngineCount   = 4;
        Appearance.WindowCount   = 3;
        Appearance.HullColor     = new Rgba(66, 74, 68);
        Appearance.AccentColor   = new Rgba(150, 120, 44);

        DefaultLoadout = new EquipmentComponent[] { EquipmentCatalog.PulseCannon, EquipmentCatalog.AdvancedShieldGenerator, EquipmentCatalog.ReinforcedHull, EquipmentCatalog.EfficientEngine, EquipmentCatalog.CargoExpander, EquipmentCatalog.CargoExpander, EquipmentCatalog.CargoExpander };
        TypicalCredits = 900;
        Price = 6000;
        Grade = 3;

        Limits.MaxWeapons = 1;
        Limits.MaxShields = 1;
        Limits.MaxPlating = 1;
        Limits.MaxEngines = 1;
        Limits.MaxUtilities = 5;
        Limits.MaxMass = 1800f;
    }
}
