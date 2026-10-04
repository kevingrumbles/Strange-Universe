using Strange_Universe.Game.Components;
using Strange_Universe.Game.NavSystem;
using Strange_Universe.Game.Systems;
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
                _activeStarSystem = new StarSystem(currentSystemNode, this, _services);
                Player.StarSystem = _activeStarSystem;
            }
            return _activeStarSystem;
        }
    }

    public List<StarSystemNode> StarSystemNodes { get; set; } = new();

    private const int NebulaPoolSize = 6;

    // Runtime services; null after deserialization until Generate(services) is called.
    private GameServices _services;

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
    /// Timed message displayed at the bottom center of the screen.
    /// </summary>
    [JsonIgnore]
    public string TimedMessage { get; private set; } = string.Empty;

    /// <summary>
    /// Remaining time in seconds for the timed message display.
    /// </summary>
    [JsonIgnore]
    public float TimedMessageRemaining { get; private set; } = 0f;

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

        // Update timed message
        if (TimedMessageRemaining > 0f)
        {
            TimedMessageRemaining -= deltaTime;
            if (TimedMessageRemaining < 0f)
                TimedMessageRemaining = 0f;
        }

        ActiveStarSystem.Update(deltaTime, input);
    }

    public void Generate(GameServices services)
    {
        _services = services;
        _activeStarSystem = null;
        GenerateNebulaPool();
        Player.Generate(services);
    }

    /// <summary>
    /// Rebuilds the active star system (e.g. after a jump) using the services supplied to
    /// <see cref="Generate(GameServices)"/>. The player is re-attached to the new system.
    /// </summary>
    public void Regenerate() => Generate(_services);

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
            {
                nebula.Dispose();
                return;
            }

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
        if (_services?.GraphicsDevice != null)
        {
            nebula.CreateTexture(_services.GraphicsDevice);
            _services.TextureCache.Register(nebula.Id, nebula.Texture);
        }
        NebulaPool.Add(nebula);
    }

    /// <summary>
    /// Displays a message at the bottom center of the screen in orange text for the specified duration.
    /// </summary>
    /// <param name="message">The text to display</param>
    /// <param name="durationSeconds">Number of seconds to display the message</param>
    public void ShowTimedMessage(string message, int durationSeconds = 3)
    {
        TimedMessage = message;
        TimedMessageRemaining = durationSeconds;
    }

    public void Dispose()
    {
        _disposed = true;

        while (_pendingNebulae.TryDequeue(out var pending))
            pending.Dispose();

        // Dispose all nebula textures owned by this universe
        foreach (var nebula in NebulaPool)
        {
            nebula?.Dispose();
        }
        NebulaPool.Clear();
    }
}
