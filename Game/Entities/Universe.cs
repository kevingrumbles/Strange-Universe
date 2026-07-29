using Strange_Universe.Game.Components;
using StrangeUniverse;
using StrangeUniverse.Game.Components;
using StrangeUniverse.Game.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json.Serialization;
using MgVector2 = Microsoft.Xna.Framework.Vector2;

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
        Player = new Player();
    }

    public void Update(float deltaTime, InputState input)
    {
        // Check if player has completed jump acceleration and left the system
        if (Player.JumpPhase == JumpPhase.Accelerate && 
            Player.Transform.Position.LengthSquared() >= ActiveStarSystem.SystemRadius * ActiveStarSystem.SystemRadius)
        {
            Player.Physics.Velocity = default;  // reset velocity before transition
            string originSystemId = Player.CurrentStarSystemID;  // Capture origin before jump
            JumpToSystem(originSystemId);
            return;
        }
        ActiveStarSystem.Update(deltaTime, input);
    }

    public void Generate()
    {
        _activeStarSystem = null;
        for (int i = NebulaPool.Count; i < NebulaPoolSize; i++)
        {
            string id = $"nebula_pool_{i}";
            var nebula = new Nebula($"{Seed}_{id}");
            Launcher.TextureCache.Register(nebula.Id, nebula.Texture);
            NebulaPool.Add(nebula);
        }
        Player.Generate();
    }

    /// <summary>
    /// Resolves the jump target, computes the world-space heading toward it, and
    /// hands control to <see cref="Player.BeginJump"/> to run the automated sequence.
    /// The actual system transition fires once the sequence completes.
    /// Falls back to a random connected system when no target is selected.
    /// </summary>
    public void BeginJump()
    {
        if (Player.JumpPhase != JumpPhase.Normal) return;
        if (ActiveStarSystem.CalculateGravityAtLocation(Player.Transform.Position) != Vector2.Zero) return;

        var connections = ActiveStarSystem.Node.SystemConnectionIds;
        if (connections == null || connections.Count == 0) return;

        // Lock in target (same resolution logic as JumpToSystem)
        string targetId;
        if (JumpRoute.Count > 0 && connections.Contains(JumpRoute[0]))
        {
            targetId = JumpRoute[0];
        }
        else
        {
            return;
        }

        StarSystemNode targetNode = StarSystemNodes.FirstOrDefault(n => n.SystemId == targetId);
        if (targetNode == null) return;

        var   dir      = targetNode.GalaxyPosition - ActiveStarSystem.Node.GalaxyPosition;
        if (dir.LengthSquared() == 0f) return;

        float jumpAngle = MathF.Atan2(dir.Y, dir.X);
        Player.BeginJump(jumpAngle);
    }

    /// <summary>
    /// Jumps the player to the next system in the route (JumpRoute[0]).
    /// Falls back to a random connected system when no route is selected.
    /// Removes the first system from the route after a successful jump.
    /// Positions the player at the system edge and initiates the arrival sequence.
    /// </summary>
    /// <param name="originSystemId">The system ID the player is jumping from, used to calculate arrival direction.</param>
    public void JumpToSystem(string originSystemId)
    {
        var connections = ActiveStarSystem.Node.SystemConnectionIds;
        if (connections == null || connections.Count == 0) return;

        // Prefer the first system in the route; fall back to random.
        string targetId = (JumpRoute.Count > 0 && connections.Contains(JumpRoute[0]))
            ? JumpRoute[0]
            : connections.ToList()[new Random().Next(connections.Count)];

        StarSystemNode targetNode = StarSystemNodes.FirstOrDefault(n => n.SystemId == targetId);
        if (targetNode == null) return;   // safety: unknown connection, do nothing

        // Get origin system node for arrival direction calculation
        StarSystemNode originNode = StarSystemNodes.FirstOrDefault(n => n.SystemId == originSystemId);

        Player.CurrentStarSystemID = targetId;

        // Remove the first system from the route after successful jump
        if (JumpRoute.Count > 0 && JumpRoute[0] == targetId)
            JumpRoute.RemoveAt(0);
        Generate();

        // Calculate arrival direction (from origin to destination)
        MgVector2 arrivalDirection;
        if (originNode != null)
        {
            // Direction from origin to destination in galaxy space (MonoGame Vector2)
            var galaxyDirection = targetNode.GalaxyPosition - originNode.GalaxyPosition;
            if (galaxyDirection.LengthSquared() > 0f)
            {
                // Normalize and invert direction so player enters from the side facing the origin
                var normalizedGalaxy = MgVector2.Normalize(galaxyDirection);
                arrivalDirection = -normalizedGalaxy;
            }
            else
            {
                // Fallback: random direction if positions are identical
                float randomAngle = (float)(new Random().NextDouble() * Math.PI * 2);
                arrivalDirection = new MgVector2((float)Math.Cos(randomAngle), (float)Math.Sin(randomAngle));
            }
        }
        else
        {
            // Fallback: random direction if origin system not found
            float randomAngle = (float)(new Random().NextDouble() * Math.PI * 2);
            arrivalDirection = new MgVector2((float)Math.Cos(randomAngle), (float)Math.Sin(randomAngle));
        }

        // Calculate entry position at system edge
        MgVector2 entryPosition = arrivalDirection * ActiveStarSystem.SystemRadius;

        // Calculate direction toward system center (inverse of arrival direction)
        MgVector2 entryDirection = -arrivalDirection;

        // Begin arrival sequence
        Player.BeginArrival(entryPosition, entryDirection, ActiveStarSystem.MandevilleRadius);
    }
}
