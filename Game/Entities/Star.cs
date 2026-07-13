using Microsoft.Xna.Framework;
using StrangeUniverse.Game.Components;
using StrangeUniverse.Rendering.ProceduralGeneration;

namespace StrangeUniverse.Game.Entities;

public class Star
{
    public string    Id { get; }
    public Transform Transform    { get; } = new();
    public float     Radius       { get; set; }
    public string    Name         { get; set; } = "Sol";
    public float     OrbitRadius  { get; set; }
    public float     OrbitAngle   { get; set; }
    public float     OrbitSpeed   { get; set; }
    public Color     MinimapColor { get; set; }
    public Star(string id, float radius, string name, Color minimapColor, float orbitRadius, float orbitAngle, float orbitSpeed)
    {
        Id = id;
        Radius = radius;
        Name = name;
        OrbitRadius = orbitRadius;
        OrbitAngle = orbitAngle;
        OrbitSpeed = orbitSpeed;

        var tex = StarTextureGenerator.Generate(minimapColor, StaticHelpers.SeedHash(Id));
        Launcher.TextureCache.Register(Id, tex);
    }
}
