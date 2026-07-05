using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace StrangeUniverse.Game.World;

public class UniverseSettings
{
    internal const string FilePath = "Data/universe-settings.json";

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

    /// <summary>
    /// Creates a new <see cref="UniverseSettings"/> with a unique <see cref="Id"/>,
    /// appends it to <c>universe-settings.json</c>, and returns it.
    /// </summary>
    public static UniverseSettings CreateDefault(string name = "New Universe", string seed = null)
    {
        var existing   = LoadExisting();
        var usedIds    = new HashSet<Guid>(existing.Select(u => u.Id));

        Guid id;
        do { id = Guid.NewGuid(); } while (usedIds.Contains(id));

        UniverseSettings settings = new UniverseSettings
        {
            Id                      = id,
            Name                    = name,
            Seed                    = seed ?? id.ToString(),
            PlanetCount             = 7,
            AsteroidCount           = 80,
            SystemRadius            = 18000f,
            AsteroidBeltInnerRadius = 4500f,
            AsteroidBeltOuterRadius = 7000f,
            BackgroundStarCount     = 600,
            StarRadius              = 180f,
            MinPlanetRadius         = 40f,
            MaxPlanetRadius         = 130f,
            MinAsteroidRadius       = 8f,
            MaxAsteroidRadius       = 32f,
        };

        existing.Add(settings);
        Persist(existing);

        return settings;
    }

    internal static List<UniverseSettings> LoadExisting()
    {
        if (!File.Exists(FilePath)) return new List<UniverseSettings>();
        try
        {
            return JsonSerializer.Deserialize<List<UniverseSettings>>(
                       File.ReadAllText(FilePath), _readOptions)
                   ?? new List<UniverseSettings>();
        }
        catch
        {
            return new List<UniverseSettings>();
        }
    }

    internal static void Persist(List<UniverseSettings> universes)
    {
            File.WriteAllText(FilePath,
                JsonSerializer.Serialize(universes, _writeOptions));
    }
}
