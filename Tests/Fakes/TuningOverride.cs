using System;
using System.Runtime.CompilerServices;
using Strange_Universe.Game.Components;

namespace Strange_Universe.Tests.Fakes;

/// <summary>
/// Lets a test run try the game with different ship tuning without editing the game's stats: set
/// <c>SU_TEST_MAXSPEED</c>, <c>SU_TEST_THRUST</c> or <c>SU_TEST_ROTATION</c> before <c>dotnet test</c> and they replace
/// the Shuttle's values for that run. Nothing is changed when they are not set. Tests must not assume particular
/// speeds; read them from the ship's <see cref="ShipStats"/> (see <see cref="Tuning"/>).
/// </summary>
public static class TuningOverride
{
    [ModuleInitializer]
    internal static void Apply()
    {
        var shuttle = ShipStats.Shuttle;
        if (Read("SU_TEST_MAXSPEED") is float maxSpeed) shuttle.MaxSpeed = maxSpeed;
        if (Read("SU_TEST_THRUST") is float thrust) shuttle.ThrustForce = thrust;
        if (Read("SU_TEST_ROTATION") is float rotation) shuttle.RotationSpeed = rotation;
    }

    private static float? Read(string name) =>
        float.TryParse(Environment.GetEnvironmentVariable(name), System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : null;
}
