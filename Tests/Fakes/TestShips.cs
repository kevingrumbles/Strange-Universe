using System.Runtime.CompilerServices;
using Strange_Universe.Game.Components;

namespace Strange_Universe.Tests.Fakes;

/// <summary>
/// A ship type with fixed numbers for the nav and combat simulations, so those tests do not change when the real ships are
/// retuned. The values are the Shuttle's from before it was retuned. Registered once, before any test runs.
/// </summary>
public static class TestShips
{
    public const string Name = "TestShuttle";

    [ModuleInitializer]
    internal static void Register()
    {
        if (ShipStats.Presets.Exists(s => s.ShipTypeName == Name))
            return;

        ShipStats.Presets.Add(new ShipStats
        {
            ShipTypeName = Name,
            ThrustForce = 360f,
            RotationSpeed = 3.5f,
            MaxSpeed = 580f,
            LinearDamping = 1f,
            Radius = 14f,
            SoftCapStart = 0.75f,
            SpriteName = "Art/Shuttle_sprite.png",
            SplashName = "Art/Shuttle_splash.png",
            SpriteScale = 1.5f,
            DefaultEquipment = new[] { "Light Laser" },
            MaxHull = 1,
            MaxShield = 1,
            MaxFuel = 5,
        });
    }
}
