using Microsoft.Xna.Framework;

namespace StrangeUniverse.Game.Components;

/// <summary>2-D physics state: velocity, per-second damping, and force accumulation.</summary>
public class PhysicsBody
{
    public Vector2 Velocity       { get; set; }
    public float   AngularVelocity { get; set; }  // radians per second (for slow-spinning asteroids)

    /// <summary>
    /// Fraction of velocity retained per second (0 = instant stop, 1 = no damping).
    /// Space drift is nearly 1; braking applies extra deceleration via force.
    /// </summary>
    public float LinearDamping { get; set; } = 1f;

    public float Mass { get; set; } = 1f;

    public void ApplyForce(Vector2 force, float deltaTime)
    {
        Velocity += force * (deltaTime / Mass);
    }

    /// <summary>
    /// Advances physics by one frame.
    /// No speed clamp is applied here — callers handle soft limiting at the force-application layer.
    /// </summary>
    public void Integrate(Transform transform, float deltaTime)
    {
        // Exponential damping: v *= damping^dt (frame-rate independent).
        // With LinearDamping == 1.0 (default) this is a no-op — true vacuum.
        float dampFactor = (float)System.Math.Pow(LinearDamping, deltaTime);
        Velocity = Velocity * dampFactor;

        transform.Position += Velocity * deltaTime;
        transform.Rotation += AngularVelocity * deltaTime;
    }
}
