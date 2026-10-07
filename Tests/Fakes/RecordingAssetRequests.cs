using System;
using System.Collections.Generic;
using Strange_Universe.Game.Components;
using Strange_Universe.Game.Entities;

namespace Strange_Universe.Tests.Fakes;

/// <summary>
/// Records every <see cref="IAssetRequests"/> call. Asteroid palette handling mirrors
/// <c>AssetService.EnsureAsteroidTexture</c>: a texture (and its seed) is only recorded for ids not created yet.
/// </summary>
public sealed class RecordingAssetRequests : IAssetRequests
{
    public List<(string Method, string Id)> Calls { get; } = new();
    public HashSet<string> CreatedPaletteIds { get; } = new();

    public RecordingAssetRequests(IEnumerable<string> preCreatedPaletteIds = null)
    {
        if (preCreatedPaletteIds != null)
            CreatedPaletteIds.UnionWith(preCreatedPaletteIds);
    }

    /// <summary>All 15 palette ids, as if another system had already been visited.</summary>
    public static RecordingAssetRequests WithAllPalettesCreated()
    {
        var ids = new string[15];
        for (int i = 0; i < ids.Length; i++) ids[i] = $"asteroid_tex_{i}";
        return new RecordingAssetRequests(ids);
    }

    public void EnsureShipArt(ShipStats shipType) => Calls.Add((nameof(EnsureShipArt), shipType?.ShipTypeName));

    public List<int> AsteroidSeeds { get; } = new();

    public void EnsureAsteroidTexture(string paletteId, int seed)
    {
        Calls.Add((nameof(EnsureAsteroidTexture), paletteId));
        if (CreatedPaletteIds.Add(paletteId))
            AsteroidSeeds.Add(seed);
    }

    public void EnsurePlanetTexture(Planet planet) => Calls.Add((nameof(EnsurePlanetTexture), planet.Id));
    public void EnsureStarTexture(Star star) => Calls.Add((nameof(EnsureStarTexture), star.Id));
    public void RegisterNebula(Nebula nebula) => Calls.Add((nameof(RegisterNebula), nebula.Id));
}
