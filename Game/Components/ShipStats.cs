using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Strange_Universe.Game.Components;

/// <summary>
/// A type of ship: its flight and durability numbers, how it looks, and the equipment it starts with.
/// Every read of a built-in type (<see cref="Shuttle"/>) builds a new instance with its own default loadout, so a ship
/// type object belongs to the one ship it is given to and is never shared. Give it to a ship with
/// <see cref="Entities.Ship.AssignShipType"/> (or a ship's constructor); the ship starts with the default loadout and can then change it.
/// </summary>
public class ShipStats
{
    public static ShipStats Shuttle => new()
    {
        ShipTypeName         = "Shuttle",
        ThrustForce          = 360f,
        RotationSpeed        = 3.5f,
        MaxSpeed             = 580f,
        LinearDamping        = 1f,
        Radius               = 14f,
        SoftCapStart         = 0.75f,
        SpriteName           = "Art/Shuttle_sprite.png",
        SplashName           = "Art/Shuttle_splash.png",
        SpriteScale          = 1.5f,
        DefaultLoadout       = new Equipment[] { EquipmentCatalog.LightLaser, EquipmentCatalog.BasicShieldGenerator, EquipmentCatalog.LightHullPlating },
        MaxHull              = 5,
        MaxFuel              = 5,
    };

    /// <summary>
    /// A new instance of the built-in type with this name (not case sensitive), falling back to the Shuttle for an unknown name.
    /// Only the name is saved, so this is used by <see cref="ShipStatsConverter"/> to restore the full type when a save is loaded.
    /// </summary>
    internal static ShipStats FromName(string shipTypeName) => Shuttle; // the Shuttle is the only built-in type so far; add the others here

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
    [JsonIgnore] public int MaxFuel { get; set; }
    [JsonIgnore] public float Mass { get; set; } = 1.0f;

    /// <summary>The equipment a ship of this type starts with. These are the objects the ship is given, so they are never shared between ships.</summary>
    [JsonIgnore] public IReadOnlyList<Equipment> DefaultLoadout { get; set; } = Array.Empty<Equipment>();
}
