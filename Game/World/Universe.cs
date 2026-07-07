using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Strange_Universe.Game.World;
using StrangeUniverse.Input;

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
    public string Seed { get; set; } = Guid.NewGuid().ToString();
    public Player Player { get; set; }

    // ── Runtime state (not serialized) ─────────────────────────────────────────────────
    [JsonIgnore]
    public StarSystem ActiveStarSystem
    {
        get
        {
            if (_activeStarSystem == null && StarSystems.Count > 0)
                _activeStarSystem = StarSystems[0];
            return _activeStarSystem;
        }
        set
        {
            _activeStarSystem = value;
        }
    }
    [JsonIgnore]
    private StarSystem _activeStarSystem { get; set;  }

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

    // ── Runtime methods ────────────────────────────────────────────────────────────────
    /// <summary>Adds a star system and makes it active if it is the first one.</summary>
    public void AddStarSystem(StarSystem system)
    {
        StarSystems.Add(system);
    }

    /// <summary>Sets the active star system. Must already belong to this universe.</summary>
    public void SetActiveStarSystem(StarSystem system) => ActiveStarSystem = system;

    /// <summary>Advances simulation for the active star system.</summary>
    public void Update(float deltaTime, InputState input) =>
        ActiveStarSystem.Update(deltaTime, input);
}
