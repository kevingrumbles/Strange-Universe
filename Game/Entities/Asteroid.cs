using StrangeUniverse.Game.Components;

namespace StrangeUniverse.Game.Entities;

public class Asteroid
{
    public Transform   Transform { get; } = new();
    public PhysicsBody Physics   { get; } = new() { LinearDamping = 1f };
    public float       Radius    { get; set; }
    public string      TextureId { get; set; } = string.Empty;

    public void Update(float deltaTime)
    {
        Physics.Integrate(Transform, deltaTime);
    }
}
