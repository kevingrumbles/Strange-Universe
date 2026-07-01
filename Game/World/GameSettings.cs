using System.IO;
using System.Text.Json;

namespace StrangeUniverse.Game.World;

/// <summary>Loads all JSON config files, falling back to in-code defaults if files are missing.</summary>
public static class GameSettings
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        AllowTrailingCommas        = true,
        ReadCommentHandling        = JsonCommentHandling.Skip,
        PropertyNameCaseInsensitive = true,
    };

    public static ShipStats   Ship   { get; private set; } = new();
    public static WorldSettings World { get; private set; } = new();
    public static CameraSettings Camera { get; private set; } = new();

    public static void Load()
    {
        Ship   = LoadFile<ShipStats>("Data/ship-stats.json")     ?? new ShipStats();
        World  = LoadFile<WorldSettings>("Data/world-settings.json") ?? new WorldSettings();
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
