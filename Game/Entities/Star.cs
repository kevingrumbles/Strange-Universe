using StrangeUniverse.Game.Components;

namespace StrangeUniverse.Game.Entities;

public class Star
{
    public Transform Transform { get; } = new();
    public float     Radius    { get; set; }
    public string    TextureId { get; set; } = string.Empty;
    public string    Name      { get; set; } = "Sol";
}
