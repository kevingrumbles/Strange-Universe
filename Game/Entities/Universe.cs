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
                StarSystemNode currentSystemNode = StarSystemNodes.FirstOrDefault(s => s.SystemId == Player.CurrentStarSystemID);
                if (currentSystemNode == null)
                {
                    // If the player's current star system ID is not found, return the first star system as a fallback.
                    currentSystemNode = StarSystemNodes.FirstOrDefault(n => n.Name == "Sol") ?? StarSystemNodes.FirstOrDefault();
                    Player.CurrentStarSystemID = currentSystemNode.SystemId; // Update player's current star system ID
                }
                _activeStarSystem = new StarSystem(currentSystemNode, this, _assets);
                Player.StarSystem = _activeStarSystem;
            }
            return _activeStarSystem;
        }
    }

    public List<StarSystemNode> StarSystemNodes { get; set; } = new();

    private const int NebulaPoolSize = 6;

    // Runtime services; null after deserialization until Generate(...) is called.
    private IAssetRequests _assets;

    /// <summary>Sink for on-screen notifications. Never null (no-op until Generate supplies one).</summary>
    [JsonIgnore]
    public IMessageSink Messages { get; private set; } = NullMessageSink.Instance;

    // Nebulae finished on a background thread, waiting to be uploaded on the main thread.
    private readonly ConcurrentQueue<Nebula> _pendingNebulae = new();
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
        DrainPendingNebulae();

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

    public void GenerateNebulaPool()
    {
        // Generate first nebula synchronously so game can start
        if (NebulaPool.Count < NebulaPoolSize)
        {
            string id = $"nebula_pool_{NebulaPool.Count}";
            var nebula = new Nebula($"{Seed}_{id}");
            UploadNebula(nebula);
        }

        // Generate remaining nebulae asynchronously in the background
        _ = GenerateRemainingNebulaPoolAsync();
    }

    /// <summary>
    /// Computes pixel data on background threads only. Textures, the texture cache and
    /// <see cref="NebulaPool"/> are touched exclusively on the main thread in
    /// <see cref="DrainPendingNebulae"/>, because MonoGame has no synchronization
    /// context and await continuations would otherwise run on the thread pool.
    /// </summary>
    private async Task GenerateRemainingNebulaPoolAsync()
    {
        int start = NebulaPool.Count;
        for (int i = start; i < NebulaPoolSize && !_disposed; i++)
        {
            int index = i;
            var nebula = await Task.Run(() => new Nebula($"{Seed}_nebula_pool_{index}"));

            if (_disposed)
                return;

            _pendingNebulae.Enqueue(nebula);
        }
    }

    /// <summary>Uploads finished nebulae to the GPU. Main thread only.</summary>
    private void DrainPendingNebulae()
    {
        while (_pendingNebulae.TryDequeue(out var nebula))
            UploadNebula(nebula);
    }

    private void UploadNebula(Nebula nebula)
    {
        _assets?.RegisterNebula(nebula);
        NebulaPool.Add(nebula);
    }

    public void Dispose()
    {
        _disposed = true;

        // Nebula textures are owned by the render-side texture cache; only drop data here.
        _pendingNebulae.Clear();
        NebulaPool.Clear();
    }
}
