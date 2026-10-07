using System;
using Microsoft.Xna.Framework.Graphics;
using Strange_Universe.Game.Components;
using Strange_Universe.Game.Entities;

namespace Strange_Universe.Game.Systems;

/// <summary>
/// Render-side owner of art loading and procedural texture creation. Wraps
/// <see cref="ArtLoader"/> and <see cref="ProceduralTextureCache"/>.
/// All methods touch the GPU and must be called on the main thread.
/// </summary>
public sealed class AssetService : IAssetRequests
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly ProceduralTextureCache _cache;

    public AssetService(GraphicsDevice graphicsDevice, ProceduralTextureCache cache)
    {
        _graphicsDevice = graphicsDevice;
        _cache = cache;
    }

    public void EnsureShipArt(ShipStats shipType)
    {
        if (shipType == null || _cache.TryGet(shipType.ShipTypeName, out _)) return;

        var tex = ArtLoader.TryLoad(_graphicsDevice, shipType.SpriteName);
        _cache.Register(shipType.ShipTypeName, tex);

        // Splash art -- fall back to the standard sprite if not provided or not found
        Texture2D splashTex = null;
        if (!string.IsNullOrEmpty(shipType.SplashName))
            splashTex = ArtLoader.TryLoad(_graphicsDevice, shipType.SplashName);
        _cache.Register(Ship.SplashArtKeyFor(shipType), splashTex ?? tex);
    }

    public void EnsureAsteroidTexture(string paletteId, int seed)
    {
        if (_cache.TryGet(paletteId, out _)) return;
        _cache.Register(paletteId, ProceduralTextures.Asteroid(_graphicsDevice, seed));
    }

    public void EnsurePlanetTexture(Planet planet)
    {
        if (_cache.TryGet(planet.Id, out _)) return;
        _cache.Register(planet.Id,
            ProceduralTextures.Planet(_graphicsDevice, planet.Type, ProceduralHelpers.SeedHash(planet.Id)));
    }

    public void EnsureStarTexture(Star star)
    {
        if (_cache.TryGet(star.Id, out _)) return;
        _cache.Register(star.Id,
            ProceduralTextures.Star(_graphicsDevice, ProceduralHelpers.StarColors[star.ColorIndex], star.CreateTextureRandom()));
    }

    public void RegisterNebula(Nebula nebula)
    {
        if (nebula.Pixels == null) return;

        var tex = new Texture2D(_graphicsDevice, Nebula.Size, Nebula.Size);
        tex.SetData(nebula.Pixels);
        _cache.Register(nebula.Id, tex);
        nebula.ReleasePixels();
    }
}
