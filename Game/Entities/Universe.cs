using Strange_Universe.Game.Components;
using Microsoft.Xna.Framework;
using Strange_Universe.Game.Components;
using Strange_Universe.Game.NavSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

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
    [JsonIgnore] public GameSettings Settings { get; set; } = new();

    private StarSystem _activeStarSystem;

    /// <summary>The system the player is in. Set by <see cref="Generate"/> and <see cref="EnterSystem"/>; has no side effects.</summary>
    [JsonIgnore]
    public StarSystem ActiveStarSystem => _activeStarSystem
        ?? throw new InvalidOperationException("No star system has been entered. Call Generate(...) first.");

    /// <summary>
    /// Builds the system for <paramref name="node"/>, makes it active, attaches the player and records
    /// it as the player's current system. Blocks until the system's nebula is uploaded.
    /// </summary>
    public StarSystem EnterSystem(StarSystemNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        // Generation is pure; everything that touches shared state happens here, in this order.
        var system = StarSystem.Create(node, this, _assets);

        // Expand the galaxy around a node only the first time it is entered.
        if (!node.Discovered)
        {
            node.Discovered = true;
            Galaxy.GenerateConnections(node, system.SystemConnectionCount, Seed);
        }

        system.AttachAssets();

        _activeStarSystem = system;
        Player.StarSystem = _activeStarSystem;
        Player.CurrentStarSystemID = node.SystemId;
        WaitForNebula(_activeStarSystem.NebulaId);
        Events.Publish(new SystemEntered(_activeStarSystem));
        return _activeStarSystem;
    }

    /// <summary>
    /// The node the player starts in: their saved system, else Sol, else the first node.
    /// Creates the default (Sol) node for a brand-new universe.
    /// </summary>
    private StarSystemNode ResolveStartNode()
    {
        if (StarSystemNodes.Count == 0)
            StarSystemNodes.Add(new StarSystemNode(Seed, position: Vector2.Zero, backConnection: null, existingNodes: StarSystemNodes));

        return Galaxy.FindById(Player.CurrentStarSystemID)
            ?? StarSystemNodes.FirstOrDefault(n => n.IsHome)
            ?? StarSystemNodes[0];
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

    // Runtime services; a no-op until Generate(...) supplies the real ones.
    private IAssetRequests _assets = NullAssetRequests.Instance;

    /// <summary>Sink for on-screen notifications. Never null (no-op until Generate supplies one).</summary>
    [JsonIgnore]
    public IMessageSink Messages { get; }

    /// <summary>Simulation events (damage, destruction, hits, system entry, messages). Never null.</summary>
    [JsonIgnore]
    public IEventBus Events { get; } = new EventBus();

    private IDisposable _messageSubscription;

    // One background generation task per pool slot, started at most once per launch.
    // Results are uploaded on the main thread (UploadFinishedNebulae / WaitForNebula).
    private readonly Task<Nebula>[] _nebulaTasks = new Task<Nebula>[NebulaPoolSize];
    private readonly bool[] _nebulaUploaded = new bool[NebulaPoolSize];
    private volatile bool _disposed;

    /// <summary>Creates nebula pixel data. Test seam: tests swap in a cheap factory.</summary>
    internal Func<string, Nebula> NebulaFactory { get; set; } = id => new Nebula(id);

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

    public Universe() { Messages = new BusMessageSink(Events); }
    public Universe(string name, string seed = null) : this()
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

    /// <summary>
    /// Starts a play session: attaches runtime services, starts nebula generation (the starting
    /// system's nebula first) and enters the player's starting system.
    /// </summary>
    public void Generate(IAssetRequests assets, IMessageSink messages = null)
    {
        assets ??= NullAssetRequests.Instance;

        // A new asset owner (new session) or a disposed universe needs its nebulae uploaded again;
        // uploaded pixels were released, so they are regenerated.
        if (_disposed || !ReferenceEquals(assets, _assets))
            ResetNebulaState();
        _disposed = false;

        _assets = assets;
        _messageSubscription?.Dispose();
        _messageSubscription = messages == null ? null
            : Events.Subscribe<MessageRequested>(m => messages.Post(m.Message, m.DurationSeconds));

        StarSystemNode start = ResolveStartNode();
        GenerateNebulaPool(StarSystemGenerator.NebulaIndexFor(start.SystemId, NebulaPoolSize));
        Player.Generate();
        _assets.EnsureShipArt(Player.ShipType);
        EnterSystem(start);
    }

    /// <summary>
    /// Starts background generation of every pool slot not already started. <paramref name="firstIndex"/>
    /// is generated first; the rest start once it has finished so they don't compete with it for CPU.
    /// Each slot is generated at most once per session.
    /// </summary>
    private void GenerateNebulaPool(int firstIndex)
    {
        StartNebula(firstIndex).ContinueWith(_ => StartRemainingNebulae(), TaskScheduler.Default);
    }

    private void StartRemainingNebulae()
    {
        if (_disposed) return;
        for (int i = 0; i < NebulaPoolSize; i++)
            StartNebula(i);
    }

    private void ResetNebulaState()
    {
        for (int i = 0; i < NebulaPoolSize; i++)
        {
            Volatile.Write(ref _nebulaTasks[i], null);
            _nebulaUploaded[i] = false;
        }
        NebulaPool.Clear();
    }

    /// <summary>
    /// Pixel data is computed on the thread pool only. Textures, the texture cache and
    /// <see cref="NebulaPool"/> are touched exclusively on the main thread, because MonoGame
    /// has no synchronization context. Thread-safe: slots may be started from a continuation.
    /// </summary>
    private Task<Nebula> StartNebula(int index)
    {
        string id = NebulaPoolId(Seed, index);
        var factory = NebulaFactory;
        var created = new Task<Nebula>(() => factory(id));
        var existing = Interlocked.CompareExchange(ref _nebulaTasks[index], created, null);
        if (existing != null) return existing;
        created.Start(TaskScheduler.Default);
        return created;
    }

    /// <summary>
    /// Blocks until the nebula <paramref name="nebulaId"/> is generated and uploaded, so a system
    /// never renders without its nebula. Main thread only.
    /// </summary>
    private void WaitForNebula(string nebulaId)
    {
        for (int i = 0; i < NebulaPoolSize; i++)
        {
            if (NebulaPoolId(Seed, i) != nebulaId) continue;
            if (_nebulaUploaded[i]) return;

            StartNebula(i).Wait();
            UploadNebula(i);
            return;
        }
    }

    /// <summary>Test hook: waits for every pool slot and uploads it. Main thread only.</summary>
    internal void WaitForAllNebulae()
    {
        for (int i = 0; i < NebulaPoolSize; i++)
            StartNebula(i).Wait();
        UploadFinishedNebulae();
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
        _assets.RegisterNebula(nebula);
        NebulaPool.Add(nebula);
    }

    public void Dispose()
    {
        _disposed = true;
        _messageSubscription?.Dispose();
        _messageSubscription = null;

        // Nebula textures are owned by the render-side texture cache; only drop data here.
        NebulaPool.Clear();
    }
}
