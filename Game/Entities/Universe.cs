using Strange_Universe.Game.Components;
using StrangeUniverse;
using StrangeUniverse.Game.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json.Serialization;

namespace Strange_Universe.Game.Entities;

/// <summary>
/// A generated universe. Owns all <see cref="StarSystem"/>s and drives the
/// update of whichever one is currently active.
/// Persisted to <c>universe-settings.json</c> with only identity fields.
/// </summary>
public class Universe
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
                    StarSystemNode defaultNode = new StarSystemNode(position: new Vector2(0,0), backConnection: null);
                    StarSystemNodes.Add(defaultNode);
                }
                StarSystemNode currentSystemNode = StarSystemNodes.FirstOrDefault(s => s.SystemId == Player.CurrentStarSystemID);
                if (currentSystemNode == null)
                {
                    // If the player's current star system ID is not found, return the first star system as a fallback.
                    currentSystemNode = StarSystemNodes.FirstOrDefault(n => n.Name == "Sol") ?? StarSystemNodes.FirstOrDefault();
                    Player.CurrentStarSystemID = currentSystemNode.SystemId; // Update player's current star system ID
                }
                _activeStarSystem = new StarSystem(currentSystemNode);
            }
            return _activeStarSystem;
        }
    }

    public List<StarSystemNode> StarSystemNodes { get; set; } = new();

    private const int NebulaPoolSize = 3;

    /// <summary>Shared nebula textures generated once per universe launch. Each StarSystem samples a crop of one of these.</summary>
    [JsonIgnore]
    public List<Nebula> NebulaPool { get; } = new();

    /// <summary>
    /// The system the player has selected on the Galaxy Map as the next jump destination.
    /// Null when no destination is selected or after a successful jump.
    /// </summary>
    [JsonIgnore]
    public string SelectedJumpTargetSystemId { get; set; } = null;

    public Universe() { }
    public Universe(string name, string seed = null)
    {
        Guid id = Guid.NewGuid();
        if (string.IsNullOrWhiteSpace(name)) name = "New Universe";

        Id = id;
        Name = name;
        Seed = seed ?? id.ToString();
        Player = new Player();
    }

    public void Update(float deltaTime, InputState input) =>
        ActiveStarSystem.Update(Player, deltaTime, input);

    public void Generate()
    {
        ClearRuntime();
        Player.Generate();
        if (NebulaPool.Count == 0)
            GenerateNebulaPool();
    }

    private void GenerateNebulaPool()
    {
        for (int i = 0; i < NebulaPoolSize; i++)
        {
            string id     = $"nebula_pool_{i}";
            var    nebula = new Nebula($"{Seed}_{id}");
            Launcher.TextureCache.Register(nebula.Id, nebula.Texture);
            NebulaPool.Add(nebula);
        }
    }

    public void ClearRuntime()
    {
        _activeStarSystem = null;
    }

    /// <summary>
    /// Jumps the player to the selected jump target (SelectedJumpTargetSystemId).
    /// Falls back to a random connected system when no target is selected.
    /// Clears the selected target after a successful jump.
    /// </summary>
    public void JumpToSystem()
    {
        var connections = ActiveStarSystem.Node.SystemConnectionIds;
        if (connections == null || connections.Count == 0) return;

        // Prefer the player-selected destination; fall back to random.
        string targetId = (!string.IsNullOrEmpty(SelectedJumpTargetSystemId) &&
                           connections.Contains(SelectedJumpTargetSystemId))
            ? SelectedJumpTargetSystemId
            : connections.ToList()[new Random().Next(connections.Count)];

        StarSystemNode targetNode = StarSystemNodes.FirstOrDefault(n => n.SystemId == targetId);
        if (targetNode == null) return;   // safety: unknown connection, do nothing

        Player.CurrentStarSystemID = targetId;
        Player.Transform.Position = System.Numerics.Vector2.Zero;
        SelectedJumpTargetSystemId = null;  // clear after jump
        Generate();
    }

    public string GetStarSystemName()
    {
        Random universeRng = new Random(StaticHelpers.SeedHash(Seed));
        string name = "Sol";
        while (name is null || StarSystemNodes.Contains(StarSystemNodes.Find(s => s.Name == name)))
        {
            name = StaticHelpers.GenerateCelestialName(StaticHelpers.CelestialNameType.System, random: universeRng);
        }
        return name;
    }
}
