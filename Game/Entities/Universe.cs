using Strange_Universe.Game.Components;
using Strange_Universe.Game.Components;
using Strange_Universe.Game.NavSystem;
using Strange_Universe;
using Strange_Universe.Game.Entities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using MgVector2 = Microsoft.Xna.Framework.Vector2;

namespace Strange_Universe.Game.Entities;

/// <summary>
/// A generated universe. Owns all <see cref="StarSystem"/>s and drives the
/// update of whichever one is currently active.
/// Persisted to <c>universe-settings.json</c> with only identity fields.
/// </summary>
public class Universe : IDisposable
{
    public Guid   Id   { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "New Universe";
    public Player Player { get; set; }
    public string Seed { get; set; }

    /// <summary>Runtime toggles supplied by the host; not persisted.</summary>
    [JsonIgnore] public Strange_Universe.Game.Systems.GameSettings Settings { get; set; } = new();

    private StarSystem _activeStarSystem;
    [JsonIgnore]
    public StarSystem ActiveStarSystem
    {
        get
        {
            if (_activeStarSystem == null)
            {
                if (StarSystemNodes.Count == 0)
                {
                    // If there are no star systems nodes, create a default one and add it to the universe.
                    StarSystemNode defaultNode = new StarSystemNode(Seed, position: new Vector2(0,0), backConnection: null, existingNodes: StarSystemNodes);
                    StarSystemNodes.Add(defaultNode);
                }
                StarSystemNode currentSystemNode = Galaxy.FindById(Player.CurrentStarSystemID);
                if (currentSystemNode == null)
                {
                    // If the player's current star system ID is not found, return the first star system as a fallback.
                    currentSystemNode = StarSystemNodes.FirstOrDefault(n => n.Name == "Sol") ?? StarSystemNodes.FirstOrDefault();
                    Player.CurrentStarSystemID = currentSystemNode.SystemId; // Update player's current star system ID
                }
                _activeStarSystem = new StarSystem(currentSystemNode, this, _assets);
                Player.StarSystem = _activeStarSystem;
                WaitForNebula(_activeStarSystem.NebulaId);
            }
            return _activeStarSystem;
        }
    }

    private List<StarSystemNode> _starSystemNodes = new();
    private GalaxyGraph _galaxy;

    public List<StarSystemNode> StarSystemNodes
    {
        get => _starSystemNodes;
        set { _starSystemNodes = value ?? new(); _galaxy = null; }
    }

    /// <summary>Indexed view over <see cref="StarSystemNodes"/> with connection generation.</summary>
    [JsonIgnore]
    public GalaxyGraph Galaxy => _galaxy ??= new GalaxyGraph(_starSystemNodes);

    public const int NebulaPoolSize = 6;

    /// <summary>Texture id of nebula <paramref name="index"/> in the pool of the universe with <paramref name="seed"/>.</summary>
    public static string NebulaPoolId(string seed, int index) => $"{seed}_nebula_pool_{index}";

    // Runtime services; null after deserialization until Generate(...) is called.
    private IAssetRequests _assets;

    /// <summary>Sink for on-screen notifications. Never null (no-op until Generate supplies one).</summary>
    [JsonIgnore]
    public IMessageSink Messages { get; private set; } = NullMessageSink.Instance;

    // One background generation task per pool slot, started at most once per launch.
    // Results are uploaded on the main thread (UploadFinishedNebulae / WaitForNebula).
    private readonly Task<Nebula>[] _nebulaTasks = new Task<Nebula>[NebulaPoolSize];
    private readonly bool[] _nebulaUploaded = new bool[NebulaPoolSize];
    private volatile bool _disposed;

    /// <summary>Shared nebula textures generated once per universe launch. Each StarSystem samples a crop of one of these.</summary>
    [JsonIgnore]
    public List<Nebula> NebulaPool { get; } = new();

    /// <summary>
    /// The route the player has selected on the Galaxy Map as a sequence of jump destinations.
    /// Empty when no route is selected. After each jump, the first system is removed from the list.
    /// </summary>
    [JsonIgnore]
    public List<string> JumpRoute { get; set; } = new();

    /// <summary>
    /// The system the player has selected on the Galaxy Map as the next jump destination.
    /// Null when no destination is selected or after a successful jump.
    /// Convenience property that returns the first system in JumpRoute, or null if empty.
    /// </summary>
    [JsonIgnore]
    public string SelectedJumpTargetSystemId
    {
        get => JumpRoute.Count > 0 ? JumpRoute[0] : null;
        set
        {
            JumpRoute.Clear();
            if (value != null)
                JumpRoute.Add(value);
        }
    }

    public Universe() { }
    public Universe(string name, string seed = null)
    {
        Guid id = Guid.NewGuid();
        if (string.IsNullOrWhiteSpace(name)) name = "New Universe";

        Id = id;
        Name = name;
        Seed = seed ?? id.ToString();
        Player = new Player(name, "Shuttle");
        Player.CurrentFuelLevel = Player.MaxFuelLevel;
        Player.CurrentHullStrength = Player.MaxHullStrength;
        Player.CurrentShieldStrength = Player.MaxShieldStrength;
        Player.Equipment.Add(Equipment.FromName("Light Laser"));
        Player.EnqueueNavTask(new SpawnTask(Player));
    }

    public void Update(float deltaTime, InputState input)
    {
        UploadFinishedNebulae();

        ActiveStarSystem.Update(deltaTime, input);
    }

    public void Generate(IAssetRequests assets, IMessageSink messages = null)
    {
        _assets = assets;
        Messages = messages ?? NullMessageSink.Instance;
        _activeStarSystem = null;
        GenerateNebulaPool();
        Player.Generate();
        _assets?.EnsureShipArt(Player.ShipType);
    }

    /// <summary>
    /// Rebuilds the active star system (e.g. after a jump) reusing the services supplied to
    /// <see cref="Generate"/>. The player is re-attached to the new system.
    /// </summary>
    public void Regenerate() => Generate(_assets, Messages);

    /// <summary>
    /// Starts background generation of every pool slot not already started. The nebula of the
    /// system the player is in is generated first; the rest start only once it has finished, so
    /// they don't compete with it for CPU. Safe to call repeatedly: each slot is generated at most
    /// once per launch.
    /// </summary>
    public void GenerateNebulaPool()
    {
        int? first = CurrentSystemNebulaIndex();
        if (first is int f)
        {
            StartNebula(f).ContinueWith(_ => StartRemainingNebulae(), TaskScheduler.Default);
            return;
        }
        StartRemainingNebulae();
    }

    private void StartRemainingNebulae()
    {
        if (_disposed) return;
        for (int i = 0; i < NebulaPoolSize; i++)
            StartNebula(i);
    }

    /// <summary>
    /// Pool slot of the system the player will enter, using the same fallback as
    /// <see cref="ActiveStarSystem"/> (Sol, then the first node) without creating anything.
    /// </summary>
    internal int? CurrentSystemNebulaIndex()
    {
        string systemId = Galaxy.FindById(Player?.CurrentStarSystemID)?.SystemId
            ?? (StarSystemNodes.FirstOrDefault(n => n.Name == "Sol") ?? StarSystemNodes.FirstOrDefault())?.SystemId;

        // New universe: no nodes yet. ActiveStarSystem will create the default node with the
        // same naming rule (StarSystemNode ctor), so predict its id the same way.
        if (systemId == null && StarSystemNodes.Count == 0)
            systemId = $"{Seed}_{NameGenerator.GetStarSystemName(Seed, StarSystemNodes)}";

        return systemId == null ? null : StarSystemGenerator.NebulaIndexFor(systemId);
    }

    /// <summary>
    /// Pixel data is computed on the thread pool only. Textures, the texture cache and
    /// <see cref="NebulaPool"/> are touched exclusively on the main thread, because MonoGame
    /// has no synchronization context. Thread-safe: slots may be started from a continuation.
    /// </summary>
    private Task<Nebula> StartNebula(int index)
    {
        string id = NebulaPoolId(Seed, index);
        var created = new Task<Nebula>(() => new Nebula(id));
        var existing = Interlocked.CompareExchange(ref _nebulaTasks[index], created, null);
        if (existing != null) return existing;
        created.Start(TaskScheduler.Default);
        return created;
    }

    /// <summary>
    /// Blocks until the nebula <paramref name="nebulaId"/> is generated and uploaded, so a system
    /// never renders without its nebula. Main thread only. No-op without assets (headless/tests).
    /// </summary>
    private void WaitForNebula(string nebulaId)
    {
        if (_assets == null || nebulaId == null) return;

        for (int i = 0; i < NebulaPoolSize; i++)
        {
            if (NebulaPoolId(Seed, i) != nebulaId) continue;
            if (_nebulaUploaded[i]) return;

            StartNebula(i).Wait();
            UploadNebula(i);
            return;
        }
    }

    /// <summary>Uploads nebulae whose background generation has finished. Main thread only.</summary>
    private void UploadFinishedNebulae()
    {
        for (int i = 0; i < NebulaPoolSize; i++)
            if (!_nebulaUploaded[i] && Volatile.Read(ref _nebulaTasks[i])?.IsCompletedSuccessfully == true)
                UploadNebula(i);
    }

    private void UploadNebula(int index)
    {
        if (_disposed) return;
        var nebula = _nebulaTasks[index].Result;
        _nebulaUploaded[index] = true;
        _assets?.RegisterNebula(nebula);
        NebulaPool.Add(nebula);
    }

    public void Dispose()
    {
        _disposed = true;

        // Nebula textures are owned by the render-side texture cache; only drop data here.
        NebulaPool.Clear();
    }
}
