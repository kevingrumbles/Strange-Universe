using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace StrangeUniverse.Game.World;

/// <summary>Loads all JSON config files, falling back to in-code defaults if files are missing.</summary>
public static class GameSettings
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        AllowTrailingCommas         = true,
        ReadCommentHandling         = JsonCommentHandling.Skip,
        PropertyNameCaseInsensitive = true,
    };

    public static ShipStats        Ship     { get; private set; } = new();
    /// <summary>All universes defined in universe-settings.json.</summary>
    public static List<UniverseSettings> Universes { get; private set; } = new();
    /// <summary>The active universe (first entry, or a generated default).</summary>
    public static UniverseSettings Universe { get; private set; } = new();
    public static CameraSettings   Camera   { get; private set; } = new();
        
    public static void Load(Guid? universeId = null)
    {
        Ship      = LoadFile<ShipStats>("Data/ship-stats.json") ?? new ShipStats();

        // UniverseSettings owns the file; delegate directly so creation and persistence
        // live in one place.
        Universes = UniverseSettings.LoadExisting();
        if (Universes.Count == 0)
        {
            // File was empty or missing — generate and persist a default universe.
            Universes.Add(UniverseSettings.CreateDefault());
        }

        Universe = universeId.HasValue
            ? Universes.FirstOrDefault(u => u.Id == universeId.Value)
              ?? UniverseSettings.CreateDefault()   // unknown ID → make a new one
            : Universes.First();

        Camera = LoadFile<CameraSettings>("Data/camera-settings.json") ?? new CameraSettings();
    }

    private static T? LoadFile<T>(string path) where T : class
    {
        if (!File.Exists(path)) return null;
        try
        {
            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<T>(json, _jsonOptions);
        }
        catch
        {
            return null;
        }
    }
}
