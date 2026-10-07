using System;
using Strange_Universe.Game.Entities;

namespace Strange_Universe.Game.Components;

/// <summary>
/// Requests the simulation makes to the render side when it needs art for something.
/// Implemented by the render-side asset service; may be <c>null</c>
/// (tests, headless), in which case callers must behave as if every request succeeded.
/// Contains no graphics types so the simulation stays graphics-free.
/// </summary>
public interface IAssetRequests
{
    /// <summary>Ensures sprite and splash art for <paramref name="shipType"/> are loaded.</summary>
    void EnsureShipArt(ShipStats shipType);

    /// <summary>
    /// Ensures the asteroid palette texture <paramref name="paletteId"/> exists.
    /// <summary>
    /// Ensures the asteroid palette texture <paramref name="paletteId"/> exists, generating it from
    /// <paramref name="seed"/> if missing. The seed is supplied by the caller so generation never
    /// depends on whether the texture already existed.
    /// </summary>
    void EnsureAsteroidTexture(string paletteId, int seed);

    /// <summary>Ensures the procedural texture for <paramref name="planet"/> exists.</summary>
    void EnsurePlanetTexture(Planet planet);

    /// <summary>Ensures the procedural texture for <paramref name="star"/> exists.</summary>
    void EnsureStarTexture(Star star);

    /// <summary>Uploads <paramref name="nebula"/> pixels to a texture. Main thread only.</summary>
    void RegisterNebula(Nebula nebula);
}

/// <summary>Ignores all asset requests. Used headless (tests) and before Generate supplies real services.</summary>
public sealed class NullAssetRequests : IAssetRequests
{
    public static readonly NullAssetRequests Instance = new();
    public void EnsureShipArt(ShipStats shipType) { }
    public void EnsureAsteroidTexture(string paletteId, int seed) { }
    public void EnsurePlanetTexture(Planet planet) { }
    public void EnsureStarTexture(Star star) { }
    public void RegisterNebula(Nebula nebula) => nebula.ReleasePixels();
}
