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

        Appearance.SpriteName  = "Art/Shuttle_sprite.png";
        Appearance.SplashName  = "Art/Shuttle_splash.png";
        Appearance.SpriteScale = 1.5f;

        DefaultLoadout = new EquipmentComponent[] { EquipmentCatalog.LightLaser, EquipmentCatalog.BasicShieldGenerator, EquipmentCatalog.LightHullPlating };
        FinishSetup();
    }
}
