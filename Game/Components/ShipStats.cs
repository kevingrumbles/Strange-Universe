using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Strange_Universe.Game.Components;

public class ShipStats
{
    public static ShipStats Shuttle => new()
    {
        ShipTypeName             = "Shuttle",
        ThrustForce          = 360f,
        RotationSpeed        = 3.5f,
        MaxSpeed             = 580f,
        LinearDamping        = 1f,
        Radius               = 14f,
        SoftCapStart         = 0.75f,
        SpriteName           = "Art/Shuttle_sprite.png",
        SplashName           = "Art/Shuttle_splash.png",
        SpriteScale          = 1.5f,
        MaxHull              = 1,
        MaxShield            = 1,
        MaxFuel              = 5,
    };

    /// <summary>All built-in ship definitions.</summary>
    public static List<ShipStats> Presets { get; } = new()
    {
        Shuttle,
    };

    /// <summary>
    /// Resolves a preset by name. Only <see cref="ShipTypeName"/> is persisted, so this
    /// is used to restore the full stat set on load.
    /// </summary>
    public static ShipStats FromName(string shipTypeName)
        => Presets.Find(s => string.Equals(s.ShipTypeName, shipTypeName, System.StringComparison.OrdinalIgnoreCase))
           ?? Shuttle;

    public string ShipTypeName { get; set; }

    [JsonIgnore] public string SpriteName { get; set; } = "Unknown";
    [JsonIgnore] public string SplashName { get; set; }
    [JsonIgnore] public float ThrustForce    { get; set; }
    [JsonIgnore] public float RotationSpeed  { get; set; }
    [JsonIgnore] public float MaxSpeed       { get; set; }
    [JsonIgnore] public float LinearDamping  { get; set; }
    [JsonIgnore] public float Radius         { get; set; }
    [JsonIgnore] public float SoftCapStart   { get; set; }
    [JsonIgnore] public float SpriteRotationOffset { get; set; } = -1.5708f;
    [JsonIgnore] public float SpriteScale { get; set; } = 1.0f;
    [JsonIgnore] public int MaxHull { get; set; }
    [JsonIgnore] public int MaxShield { get; set; }
    [JsonIgnore] public int MaxFuel { get; set; }
    [JsonIgnore] public float Mass { get; set; } = 1.0f;
}
