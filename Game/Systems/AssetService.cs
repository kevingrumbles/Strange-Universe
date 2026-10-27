using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework.Graphics;
using Strange_Universe.Game.Components;
using Strange_Universe.Game.Components.ShipComponents;
using Strange_Universe.Game.Entities;
using Strange_Universe.Game.Entities.ShipParts;

namespace Strange_Universe.Game.Systems;

/// <summary>
/// Render-side owner of every texture: art loaded from the Art folder, procedurally generated textures, and the nebulae. Textures are kept by
/// id, so the renderers look them up here (<see cref="TryGet"/>). All methods touch the GPU and must be called on the main thread.
/// Created for a play session and disposed with it.
/// </summary>
public sealed class AssetService : IAssetRequests, IDisposable
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly Dictionary<string, Texture2D> _textures = new();

    public AssetService(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;
    }

    // -- Textures by id -------------------------------------------------------

    /// <summary>Keeps <paramref name="texture"/> under <paramref name="id"/>, disposing any texture already kept there.</summary>
    public void Register(string id, Texture2D texture)
    {
        if (id is null) return;

        if (_textures.TryGetValue(id, out var existing))
            existing.Dispose();
        _textures[id] = texture;
    }

    public bool TryGet(string id, out Texture2D texture) => _textures.TryGetValue(id, out texture);

    /// <summary>
    /// Loads a PNG from <paramref name="relativePath"/> (relative to the executable directory), read straight from disk so replacing a file shows on
    /// the next run. The caller owns the texture. Returns null if the file does not exist.
    /// </summary>
    public Texture2D LoadArt(string relativePath)
    {
        string fullPath = Strange_Universe.Persistence.ResolveDataPath(relativePath);

        if (!File.Exists(fullPath))
        {
            Console.WriteLine($"[AssetService] File not found: {fullPath}");
            return null;
        }

        using var stream = File.OpenRead(fullPath);
        return Texture2D.FromStream(_graphicsDevice, stream);
    }

    // -- IAssetRequests ---------------------------------------------------------

    public void EnsureShipArt(ShipStats shipType)
    {
        if (shipType == null || TryGet(shipType.ShipTypeName, out _)) return;

        var tex = LoadArt(shipType.Appearance.SpriteName);
        Register(shipType.ShipTypeName, tex);

        // Splash art -- fall back to the standard sprite if not provided or not found
        Texture2D splashTex = null;
        if (!string.IsNullOrEmpty(shipType.Appearance.SplashName))
            splashTex = LoadArt(shipType.Appearance.SplashName);
        Register(Ship.SplashArtKeyFor(shipType), splashTex ?? tex);
    }

    public void EnsureAsteroidTexture(string paletteId, int seed)
    {
        if (TryGet(paletteId, out _)) return;
        Register(paletteId, ProceduralTextures.Asteroid(_graphicsDevice, seed));
    }

    public void EnsurePlanetTexture(Planet planet)
    {
        if (TryGet(planet.Id, out _)) return;
        Register(planet.Id,
            ProceduralTextures.Planet(_graphicsDevice, planet.Type, ProceduralHelpers.SeedHash(planet.Id)));
    }

    public void EnsureStarTexture(Star star)
    {
        if (TryGet(star.Id, out _)) return;
        Register(star.Id,
            ProceduralTextures.Star(_graphicsDevice, ProceduralHelpers.StarColors[star.ColorIndex], star.CreateTextureRandom()));
    }

    public void RegisterNebula(Nebula nebula)
    {
        if (nebula.Pixels == null) return;

        var tex = new Texture2D(_graphicsDevice, Nebula.Size, Nebula.Size);
        tex.SetData(nebula.Pixels);
        Register(nebula.Id, tex);
        nebula.ReleasePixels();
    }

    public void Dispose()
    {
        foreach (var texture in _textures.Values)
            texture?.Dispose();
        _textures.Clear();
    }
}
