using System;
using Strange_Universe.Game.Entities;

namespace Strange_Universe.Game.Components;

/// <summary>
/// Requests the simulation makes to the render side when it needs art for something.
/// Implemented by <c>Strange_Universe.Game.Systems.AssetService</c>; may be <c>null</c>
/// (tests, headless), in which case callers must behave as if every request succeeded.
/// Contains no graphics types so the simulation stays graphics-free.
/// </summary>
public interface IAssetRequests
{
    /// <summary>Ensures sprite and splash art for <paramref name="shipType"/> are loaded.</summary>
    void EnsureShipArt(ShipStats shipType);

    /// <summary>
    /// Ensures the asteroid palette texture <paramref name="paletteId"/> exists.
    /// <paramref name="nextSeed"/> is invoked only when the texture has to be created.
    /// </summary>
    void EnsureAsteroidTexture(string paletteId, Func<int> nextSeed);

    /// <summary>Ensures the procedural texture for <paramref name="planet"/> exists.</summary>
    void EnsurePlanetTexture(Planet planet);

    /// <summary>Ensures the procedural texture for <paramref name="star"/> exists.</summary>
    void EnsureStarTexture(Star star);

    /// <summary>Uploads <paramref name="nebula"/> pixels to a texture. Main thread only.</summary>
    void RegisterNebula(Nebula nebula);
}

/// <summary>Destination for short on-screen notifications.</summary>
public interface IMessageSink
{
    void Post(string message, int durationSeconds = 3);
}

/// <summary>Discards messages. Used when no HUD is attached (tests, before Generate).</summary>
public sealed class NullMessageSink : IMessageSink
{
    public static readonly NullMessageSink Instance = new();
    public void Post(string message, int durationSeconds = 3) { }
}

/// <summary>Virtual tile used for parallax background stars. Shared by generation and rendering.</summary>
public static class BackgroundTile
{
    public const float Width = 1920f;
    public const float Height = 1080f;
}
