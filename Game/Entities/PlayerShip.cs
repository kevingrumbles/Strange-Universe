using Microsoft.Xna.Framework;
using StrangeUniverse.Game.Components;
using StrangeUniverse.Game.World;
using StrangeUniverse.Input;

namespace StrangeUniverse.Game.Entities;

/// <summary>The player-controlled ship.  Pure game logic — no MonoGame rendering types.</summary>
public class PlayerShip
{
    public Transform   Transform { get; } = new();
    public PhysicsBody Physics   { get; }
    public string      TextureId { get; set; } = string.Empty;
    public float       Radius    { get; set; }

    private readonly ShipStats _stats;

    public PlayerShip(ShipStats stats)
    {
        _stats = stats;
        Radius = stats.Radius;
        Physics = new PhysicsBody
        {
            LinearDamping = stats.LinearDamping,
            Mass = 1f,
        };
    }

    public void Update(float deltaTime, InputState input)
    {
        // Rotation
        if (input.RotateLeft)
            Transform.Rotation -= _stats.RotationSpeed * deltaTime;
        if (input.RotateRight)
            Transform.Rotation += _stats.RotationSpeed * deltaTime;

        // Thrust
        if (input.Thrust)
            Physics.ApplyForce(Transform.Forward * _stats.ThrustForce, deltaTime);

        // Brake: apply force opposing current velocity
        if (input.Brake && Physics.Velocity.LengthSquared() > 0.01f)
        {
            Vector2 brakeDir = -Vector2.Normalize(Physics.Velocity);
            Physics.ApplyForce(brakeDir * _stats.BrakeForce, deltaTime);
        }

        Physics.Integrate(Transform, deltaTime, _stats.MaxSpeed);
    }
}
