using System.Linq;
using Microsoft.Xna.Framework;
using Strange_Universe.Game.Entities;
using Strange_Universe.Tests.Fakes;
using Xunit;

namespace Strange_Universe.Tests;

/// <summary>
/// Round 2, Phase 0: characterization of the real (asset-backed) generation path.
/// Skipped tests document known bugs; they are un-skipped by the phase that fixes them.
/// </summary>
public class GenerationCharacterizationTests
{
    private const string Seed = "char-seed";

    private static (Universe universe, StarSystemNode node) BuildUniverse(string seed = Seed)
    {
        var universe = new Universe("Char", seed);
        var node = new StarSystemNode(seed, Vector2.Zero, existingNodes: universe.StarSystemNodes);
        universe.StarSystemNodes.Add(node);
        universe.Player.CurrentStarSystemID = node.SystemId;
        return (universe, node);
    }

    private static Vector2[] AsteroidLayout(IAssetRequestsFactory make)
    {
        var (universe, node) = BuildUniverse();
        var system = StarSystem.Create(node, universe, make());
        return system.Asteroids.Select(a => a.Position).ToArray();
    }

    private delegate Strange_Universe.Game.Components.IAssetRequests IAssetRequestsFactory();

    [Fact]
    public void Fake_DrawsSeedOnlyForNewPalettes()
    {
        var (universe, node) = BuildUniverse();
        var fresh = new RecordingAssetRequests();
        StarSystem.Create(node, universe, fresh).AttachAssets();

        Assert.Equal(15, fresh.Calls.Count(c => c.Method == "EnsureAsteroidTexture"));
        Assert.Equal(15, fresh.CreatedPaletteIds.Count);
    }

    [Fact]
    public void AsteroidLayout_IndependentOfVisitOrder()
    {
        var firstVisit  = AsteroidLayout(() => new RecordingAssetRequests());
        var laterVisit  = AsteroidLayout(RecordingAssetRequests.WithAllPalettesCreated);

        Assert.Equal(firstVisit, laterVisit);
    }

    [Fact]
    public void AsteroidLayout_HeadlessMatchesGamePath()
    {
        var headless = AsteroidLayout(() => null);
        var game     = AsteroidLayout(RecordingAssetRequests.WithAllPalettesCreated);

        Assert.Equal(headless, game);
    }

    [Fact]
    public void NebulaSelection_IndependentOfPoolFill()
    {
        string Select(int poolCount)
        {
            var (universe, node) = BuildUniverse();
            for (int i = 0; i < poolCount; i++)
                universe.NebulaPool.Add(Nebula.CreateWithoutPixels($"{Seed}_nebula_pool_{i}"));
            return StarSystem.Create(node, universe, null).NebulaId;
        }

        Assert.Equal(Select(6), Select(1));
    }

    [Fact]
    public void PaletteSeeds_DependOnUniverseSeedOnly()
    {
        var (u1, n1) = BuildUniverse();
        var a = new RecordingAssetRequests();
        StarSystem.Create(n1, u1, a).AttachAssets();

        // A different system in the same universe requests the same palette seeds.
        var (u2, _) = BuildUniverse();
        var other = new StarSystemNode { SystemId = $"{Seed}_Other", Name = "Other", GalaxyPosition = new Vector2(3, 3) };
        u2.StarSystemNodes.Add(other);
        var b = new RecordingAssetRequests();
        StarSystem.Create(other, u2, b).AttachAssets();

        Assert.Equal(15, a.AsteroidSeeds.Count);
        Assert.Equal(a.AsteroidSeeds, b.AsteroidSeeds);
    }

    [Fact]
    public void NebulaId_IsAlwaysSet_EvenWithEmptyPool()
    {
        var (universe, node) = BuildUniverse();
        var s = StarSystem.Create(node, universe, null);
        Assert.StartsWith($"{Seed}_nebula_pool_", s.NebulaId);
    }

    [Fact]
    public void EnteringSystem_WaitsForItsNebula()
    {
        var universe = new Universe("Char", Seed) { NebulaFactory = Nebula.CreateWithoutPixels };
        var assets = new RecordingAssetRequests();
        universe.Generate(assets);

        var system = universe.ActiveStarSystem;

        Assert.Contains(("RegisterNebula", system.NebulaId), assets.Calls);
        Assert.Contains(universe.NebulaPool, n => n.Id == system.NebulaId);
        universe.Dispose();
    }

    [Fact]
    public void NebulaIndexFor_MatchesSelectedNebula()
    {
        var (universe, node) = BuildUniverse();
        var s = StarSystem.Create(node, universe, null);
        Assert.Equal(Universe.NebulaPoolId(Seed, StarSystemGenerator.NebulaIndexFor(node.SystemId, Universe.NebulaPoolSize)), s.NebulaId);
    }

    [Fact]
    public void NewUniverse_StartingNebulaIsRequestedFirst()
    {
        var universe = new Universe("Fresh", "fresh-seed") { NebulaFactory = Nebula.CreateWithoutPixels };
        Assert.Empty(universe.StarSystemNodes);
        var assets = new RecordingAssetRequests();

        universe.Generate(assets);

        string firstRegistered = assets.Calls.First(c => c.Method == "RegisterNebula").Id;
        Assert.Equal(universe.ActiveStarSystem.NebulaId, firstRegistered);
        universe.Dispose();
    }

    [Fact]
    public void ConstructingSameSystemTwice_DoesNotChangeUniverse()
    {
        var (universe, node) = BuildUniverse();
        universe.NebulaFactory = Nebula.CreateWithoutPixels;
        universe.EnterSystem(node);

        string before = Snapshot(universe);
        universe.EnterSystem(node);

        Assert.Equal(before, Snapshot(universe));
    }

    private static string Snapshot(Universe u) => string.Join("|", u.StarSystemNodes.Select(n =>
        $"{n.SystemId}@{n.GalaxyPosition}:{n.Discovered}:[{string.Join(",", n.SystemConnectionIds.OrderBy(x => x))}]"));
}
