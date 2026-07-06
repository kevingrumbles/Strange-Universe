using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
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

    // ── Runtime state (not serialized) ─────────────────────────────────────────────────
    [JsonIgnore]
    public StarSystem ActiveStarSystem { get; private set; } = null!;

    private readonly List<StarSystem> _starSystems = new();

    // ── Factory ────────────────────────────────────────────────────────────────────────
    /// <summary>
    /// Creates a new <see cref="Universe"/> with a unique <see cref="Id"/> and returns it.
    /// </summary>
    public static Universe CreateDefault(string name = "New Universe", string seed = null)
    {
        Guid id = Guid.NewGuid();

        return new Universe
        {
            Id   = id,
            Name = name,
            Seed = seed ?? id.ToString(),
        };
    }

    // ── Runtime methods ────────────────────────────────────────────────────────────────
    /// <summary>Adds a star system and makes it active if it is the first one.</summary>
    public void AddStarSystem(StarSystem system)
    {
        _starSystems.Add(system);
        if (_starSystems.Count == 1)
            ActiveStarSystem = system;
    }

    /// <summary>Sets the active star system. Must already belong to this universe.</summary>
    public void SetActiveStarSystem(StarSystem system) => ActiveStarSystem = system;

    /// <summary>Advances simulation for the active star system.</summary>
    public void Update(float deltaTime, InputState input) =>
        ActiveStarSystem.Update(deltaTime, input);
}
