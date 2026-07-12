using Strange_Universe.Game.World;
using StrangeUniverse.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace StrangeUniverse.Game.World;

/// <summary>
/// A generated universe. Owns all <see cref="StarSystem"/>s and drives the
/// update of whichever one is currently active.
/// Persisted to <c>universe-settings.json</c> with only identity fields.
/// </summary>
public class Universe
{
    // ── Serialized identity (saved to / loaded from JSON) ──────────────────────────────
    public Guid   Id   { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "New Universe";
    public Player Player { get; set; }

    // ── Runtime state (not serialized) ─────────────────────────────────────────────────
    private string _seed;
    public string Seed
    {
        get => _seed;
        set
        {
            _universeHash = StaticHelpers.SeedHash(value);
            _universeRng = new Random(_universeHash);
            _seed = value;
        }
    }
    private int _universeHash { get; set; }
    private Random _universeRng { get; set; }
    [JsonIgnore]
    public StarSystem ActiveStarSystem
    {
        get
        {
            StarSystem currentSystem = StarSystems.FirstOrDefault(s => s.SystemId == Player.CurrentStarSystemID);
            if (currentSystem == null)
            {
                if (StarSystems.Count == 0)
                {
                    // If there are no star systems, create a default one and add it to the universe.
                    CreateDefaultStarSystem();
                }

                // If the player's current star system ID is not found, return the first star system as a fallback.
                currentSystem = StarSystems.FirstOrDefault();
                Player.CurrentStarSystemID = currentSystem?.SystemId ?? Guid.Empty; // Update player's current star system ID
            }
            return currentSystem;
        }
    }

    public List<StarSystem> StarSystems { get; set; } = new();

    // ── Factory ────────────────────────────────────────────────────────────────────────
    /// <summary>
    /// Creates a new <see cref="Universe"/> with a unique <see cref="Id"/> and returns it.
    /// </summary>
    public static Universe CreateDefault(string name = "New Universe", string seed = null)
    {
        Guid id = Guid.NewGuid();

        return new Universe
        {
            Id = id,
            Name = name,
            Seed = seed ?? id.ToString(),
            Player = new Player()
        };
    }

    public void CreateDefaultStarSystem()
    {
        StarSystem starSystem = new StarSystem();
        StarSystems.Add(starSystem);
    }

    // ── Runtime methods ────────────────────────────────────────────────────────────────
    /// <summary>Advances simulation for the active star system.</summary>
    public void Update(float deltaTime, InputState input) =>
        ActiveStarSystem.Update(Player, deltaTime, input);

    // ── Generation ────────────────────────────────────────────────────────────

    /// <summary>Procedurally generates a star system and adds it to this universe.</summary>
    public void Generate()
    {
        ActiveStarSystem.Generate();
    }
}
