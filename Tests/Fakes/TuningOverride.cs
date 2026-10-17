using System;
using System.Runtime.CompilerServices;
using Strange_Universe.Game.Components.ShipComponents;

namespace Strange_Universe.Tests.Fakes;

/// <summary>
/// Lets a test run try the game with different ship tuning without editing the game's stats: set
/// <c>SU_TEST_MAXSPEED</c>, <c>SU_TEST_THRUST</c> or <c>SU_TEST_ROTATION</c> before <c>dotnet test</c> and they replace
/// the flight numbers of every new ship type for that run. Nothing is changed when they are not set. Tests must not assume particular
/// speeds; read them from the ship's <see cref="ShipStats"/> (see <see cref="Tuning"/>).
/// </summary>
public static class TuningOverride
{
    [ModuleInitializer]
    internal static void Apply()
    {
        float? maxSpeed = Read("SU_TEST_MAXSPEED"), thrust = Read("SU_TEST_THRUST"), rotation = Read("SU_TEST_ROTATION");
        if (maxSpeed is null && thrust is null && rotation is null)
            return;

        // Every new ship type gets the overridden flight numbers.
        ShipStats.FlightTuning = flight =>
        {
            if (maxSpeed is float m) flight.MaxSpeed = m;
            if (thrust is float t) flight.ThrustForce = t;
            if (rotation is float r) flight.RotationSpeed = r;
        };
    }

    private static float? Read(string name) =>
        float.TryParse(Environment.GetEnvironmentVariable(name), System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : null;
}
