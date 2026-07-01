using System;
using Microsoft.Xna.Framework;

namespace StrangeUniverse.Game.Components;

/// <summary>Stores world position, rotation (radians), and uniform scale for any entity.</summary>
public class Transform
{
    public Vector2 Position { get; set; }
    public float   Rotation { get; set; }   // radians; 0 = pointing right (+X)
    public float   Scale    { get; set; } = 1f;

    /// <summary>Unit vector in the direction the entity is facing.</summary>
    public Vector2 Forward =>
        new Vector2((float)Math.Cos(Rotation), (float)Math.Sin(Rotation));
}
