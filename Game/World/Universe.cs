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
/// Also carries the configuration used to generate it and handles persistence
/// to <c>universe-settings.json</c>.
/// </summary>
public class Universe
{
    // ── Persistence ───────────────────────────────────────────────────────────
    public static string FilePath = "Data/universe-settings.json";

    private static readonly JsonSerializerOptions _readOptions = new()
    {
        AllowTrailingCommas         = true,
        ReadCommentHandling         = JsonCommentHandling.Skip,
        PropertyNameCaseInsensitive = true,
    };

    private static readonly JsonSerializerOptions _writeOptions = new()
    {
        WriteIndented = true,
    };

    // ── Serialized configuration (saved to / loaded from JSON) ────────────────
    public Guid   Id                      { get; set; } = Guid.NewGuid();
    public string Name                    { get; set; } = "New Universe";
    public string Seed                    { get; set; } = Guid.NewGuid().ToString();
    public int    PlanetCount             { get; set; } = 7;
    public int    AsteroidCount           { get; set; } = 80;
    public float  SystemRadius            { get; set; } = 18000f;
    public float  AsteroidBeltInnerRadius { get; set; } = 4500f;
    public float  AsteroidBeltOuterRadius { get; set; } = 7000f;
    public int    BackgroundStarCount     { get; set; } = 600;
    public float  StarRadius              { get; set; } = 180f;
    public float  MinPlanetRadius         { get; set; } = 40f;
    public float  MaxPlanetRadius         { get; set; } = 130f;
    public float  MinAsteroidRadius       { get; set; } = 8f;
    public float  MaxAsteroidRadius       { get; set; } = 32f;

    // ── Runtime state (not serialized) ────────────────────────────────────────
    [JsonIgnore]
    public IReadOnlyList<StarSystem> StarSystems => _starSystems;

    [JsonIgnore]
    public StarSystem ActiveStarSystem { get; private set; } = null!;

    private readonly List<StarSystem> _starSystems = new();

    // ── Factory / persistence ─────────────────────────────────────────────────
    /// <summary>
    /// Creates a new <see cref="Universe"/> with a unique <see cref="Id"/>,
    /// appends it to <c>universe-settings.json</c>, and returns it.
    /// </summary>
    public static Universe CreateDefault(string name = "New Universe", string seed = null)
    {

        Guid id = Guid.NewGuid();

        var universe = new Universe
        {
            Id   = id,
            Name = name,
            Seed = seed ?? id.ToString(),
        };

        return universe;
    }

    public static List<Universe> LoadExisting()
    {
        if (!File.Exists(FilePath)) return new List<Universe>();
        try
        {
            return JsonSerializer.Deserialize<List<Universe>>(
                       File.ReadAllText(FilePath), _readOptions)
                   ?? new List<Universe>();
        }
        catch
        {
            return new List<Universe>();
        }
    }

    public static void Persist(List<Universe> universes)
    {
        File.WriteAllText(FilePath,
            JsonSerializer.Serialize(universes, _writeOptions));
    }

    // ── Runtime methods ───────────────────────────────────────────────────────
    /// <summary>Adds a star system and makes it active if it is the first one.</summary>
    public void AddStarSystem(StarSystem system)
    {
        _starSystems.Add(system);
        if (_starSystems.Count == 1)
            ActiveStarSystem = system;
    }

    /// <summary>
    /// Sets the active star system. The system must already belong to this universe.
    /// </summary>
    public void SetActiveStarSystem(StarSystem system) => ActiveStarSystem = system;

    /// <summary>Advances simulation for the active star system.</summary>
    public void Update(float deltaTime, InputState input) =>
        ActiveStarSystem.Update(deltaTime, input);
}
