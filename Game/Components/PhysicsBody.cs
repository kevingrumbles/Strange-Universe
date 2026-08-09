using Microsoft.Xna.Framework;

namespace StrangeUniverse.Game.Components;

/// <summary>2-D physics state: velocity, per-second damping, and force accumulation.</summary>
public class PhysicsBody
{
    public Vector2 Velocity       { get; set; }
    public float   AngularVelocity { get; set; }  // radians per second (for slow-spinning asteroids)

    public float Mass { get; set; } = 1f;

    public void ApplyForce(Vector2 force, float deltaTime)
    {
        if (force != Vector2.Zero)
            Velocity += force * (deltaTime / Mass);
    }

    /// <summary>
    /// Advances physics by one frame.
    /// No speed clamp is applied here — callers handle soft limiting at the force-application layer.
    /// </summary>
    public void Integrate(Transform transform, float deltaTime)
    {
        transform.Position += Velocity * deltaTime;
        transform.Rotation += AngularVelocity * deltaTime;
    }
}
