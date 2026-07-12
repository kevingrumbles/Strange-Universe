using Microsoft.Xna.Framework;
using StrangeUniverse.Game.Components;

namespace StrangeUniverse.Game.Entities;

public class Star
{
    public Transform Transform    { get; } = new();
    public float     Radius       { get; set; }
    public string    TextureId    { get; set; } = string.Empty;
    public string    Name         { get; set; } = "Sol";
    public Color     MinimapColor { get; set; } = new Color(255, 230, 100);
    public float     OrbitRadius  { get; set; }
    public float     OrbitAngle   { get; set; }
    public float     OrbitSpeed   { get; set; }
}
