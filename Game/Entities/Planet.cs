using StrangeUniverse.Game.Components;

namespace StrangeUniverse.Game.Entities;

public class Planet
{
    public Transform Transform { get; } = new();
    public float     Radius    { get; set; }
    public string    Name      { get; set; } = string.Empty;
    public string    TextureId { get; set; } = string.Empty;
}
