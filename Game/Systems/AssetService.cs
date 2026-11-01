using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
    private readonly Dictionary<string, ShipHull> _hulls = new();
    private readonly Dictionary<string, Texture2D> _splashFiles = new(); // splash pictures read from files, shared by the types that use them

    /// <summary>Texture id of the engine flame shared by every ship.</summary>
    public const string FlameId = "ship_flame";
    public const string ScorchId = "ship_scorch";

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

    /// <summary>The generated ship for a ship type name: where its engines are.</summary>
    public bool TryGetHull(string shipTypeName, out ShipHull hull) => _hulls.TryGetValue(shipTypeName, out hull);

    /// <summary>
    /// Loads a PNG from <paramref name="relativePath"/> (relative to the executable directory), read straight from disk so replacing a file shows on
    /// the next run. The caller owns the texture. Returns null if the file does not exist.
    /// </summary>
    public Texture2D LoadArt(string relativePath)
    {
        string fullPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, relativePath));

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

        RegisterShipArt(shipType, ShipHullGenerator.Generate(shipType.Appearance));
    }

    // Hulls made in the background wait here (pixels only; no GPU use) until the main thread uploads them.
    private readonly ConcurrentQueue<(ShipStats Type, ShipHull Hull)> _preloadedHulls = new();

    /// <summary>Makes the hulls of the ship types that have none, one after another on a background thread. See <see cref="UploadPreloadedShipArt"/>.</summary>
    public void PreloadShipArt(IReadOnlyList<ShipStats> shipTypes)
    {
        var missing = shipTypes.Where(type => !TryGet(type.ShipTypeName, out _)).ToList();
        if (missing.Count == 0) return;

        Task.Run(() =>
        {
            foreach (var type in missing)
                _preloadedHulls.Enqueue((type, ShipHullGenerator.Generate(type.Appearance)));
        });
    }

    /// <summary>Uploads one preloaded hull, if one is ready: a frame's worth of the work, so there is no hitch. Call every frame, on the main thread.</summary>
    public void UploadPreloadedShipArt()
    {
        if (!_preloadedHulls.TryDequeue(out var made)) return;
        if (TryGet(made.Type.ShipTypeName, out _)) return; // the game needed this one first and made it itself

        RegisterShipArt(made.Type, made.Hull);
    }

    private void RegisterShipArt(ShipStats shipType, ShipHull hull)
    {
        var tex = new Texture2D(_graphicsDevice, ShipHull.AtlasWidth, ShipHull.Size); // every lighting direction, side by side
        tex.SetData(hull.Pixels);
        Register(shipType.ShipTypeName, tex);
        _hulls[shipType.ShipTypeName] = hull;

        if (!TryGet(FlameId, out _))
        {
            var flame = new Texture2D(_graphicsDevice, ShipHullGenerator.FlameWidth, ShipHullGenerator.FlameHeight);
            flame.SetData(ShipHullGenerator.Flame());
            Register(FlameId, flame);

            var scorch = new Texture2D(_graphicsDevice, ShipHullGenerator.ScorchSize, ShipHullGenerator.ScorchSize);
            scorch.SetData(ShipHullGenerator.Scorch());
            Register(ScorchId, scorch);
        }

        // Splash art -- fall back to the generated hull if not provided or not found. A file is read once, however many types show it.
        Texture2D splashTex = null;
        string splashName = shipType.Appearance.SplashName;
        if (!string.IsNullOrEmpty(splashName) && !_splashFiles.TryGetValue(splashName, out splashTex))
            _splashFiles[splashName] = splashTex = LoadArt(splashName);
        if (splashTex == null)
        {
            splashTex = new Texture2D(_graphicsDevice, ShipHull.Size, ShipHull.Size);
            splashTex.SetData(hull.Picture(0));
        }
        Register(Ship.SplashArtKeyFor(shipType), splashTex);
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
            texture?.Dispose(); // a shared splash picture is disposed once per type that shows it, which is harmless
        _textures.Clear();
        _splashFiles.Clear();
    }
}
