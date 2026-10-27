using System;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;

namespace Strange_Universe.Game.Components;

/// <summary>
/// Where something is in the world: position, rotation (radians) and uniform scale. A ship's physics holds one; stars, planets and asteroids are one
/// (they derive from it). None of it is written to a save.
/// </summary>
public class Transform
{
    [JsonIgnore] public Vector2 Position { get; set; }
    [JsonIgnore] public float   Rotation { get; set; }   // radians; 0 = pointing right (+X)
    [JsonIgnore] public float   Scale    { get; set; } = 1f;

    /// <summary>Unit vector in the direction the entity is facing.</summary>
    [JsonIgnore] public Vector2 Forward =>
        new Vector2((float)Math.Cos(Rotation), (float)Math.Sin(Rotation));
}
