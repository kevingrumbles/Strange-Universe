using System;
using Microsoft.Xna.Framework;
using Strange_Universe.Game.Components;

namespace Strange_Universe.Game.Entities.ShipParts;

/// <summary>
/// Ship movement: soft-capped thrust, rotation, gravity application and integration.
/// Owns the ship's <see cref="Components.Transform"/> and <see cref="PhysicsBody"/>.
/// </summary>
public class ShipPhysics
{
    private readonly Func<ShipStats> _stats;

    public ShipPhysics(Func<ShipStats> stats)
    {
        _stats = stats ?? throw new ArgumentNullException(nameof(stats));
    }

    public Transform Transform { get; } = new();
    public PhysicsBody Body { get; } = new();

    private ShipStats Stats => _stats();

    /// <summary>
    /// Applies thrust force in the ship's current facing direction.
    /// Uses a soft speed cap that gradually reduces acceleration-contributing thrust as MaxSpeed is approached.
    /// Lateral and decelerating components are never reduced.
    /// </summary>
    public void ApplyThrust(float deltaTime)
    {
        ShipStats stats = Stats;
        Vector2 thrustForce = Transform.Forward * stats.ThrustForce;
        Vector2 velocity = Body.Velocity;
        float currentSpeed = velocity.Length();

        if (currentSpeed > 0f)
        {
            Vector2 velDir = velocity / currentSpeed;
            float parallelMag = Vector2.Dot(thrustForce, velDir);

            // Only reduce thrust that would push speed higher (positive parallel component)
            if (parallelMag > 0f)
            {
                float softStart = stats.MaxSpeed * stats.SoftCapStart;

                if (currentSpeed >= stats.MaxSpeed)
                {
                    // At or above max: strip the forward component entirely.
                    // The ship can still turn — lateral thrust is unaffected.
                    thrustForce -= velDir * parallelMag;
                }
                else if (currentSpeed > softStart)
                {
                    // Soft zone: linearly fade the forward component to zero.
                    float t = (currentSpeed - softStart) / (stats.MaxSpeed - softStart);
                    thrustForce -= velDir * (parallelMag * t);
                }
                // Below softStart: full thrust, no reduction
            }
            // Negative parallel (decelerating) and lateral components: never reduced
        }

        Body.ApplyForce(thrustForce, deltaTime);
    }

    /// <summary>
    /// Rotates towards a target angle. Returns true if already aligned within the threshold.
    /// </summary>
    public bool RotateTowards(float targetAngle, float deltaTime, float threshold = float.MaxValue)
    {
        if (float.IsNaN(targetAngle) || float.IsInfinity(targetAngle)) return false;
        if (float.IsNaN(Transform.Rotation) || float.IsInfinity(Transform.Rotation))
            Transform.Rotation = 0f;

        float diff = MathHelpers.WrapAngle(targetAngle - Transform.Rotation);
        float maxDelta = Stats.RotationSpeed * deltaTime;

        if (Math.Abs(diff) <= Math.Min(maxDelta, threshold))
        {
            Transform.Rotation = targetAngle;
            return true;
        }

        Transform.Rotation += Math.Sign(diff) * maxDelta;
        return false;
    }

    /// <summary>Applies manual rotation input.</summary>
    public void ApplyRotation(Direction d, float deltaTime)
    {
        switch (d)
        {
            case Direction.Left:
                Transform.Rotation -= Stats.RotationSpeed * deltaTime;
                break;
            case Direction.Right:
                Transform.Rotation += Stats.RotationSpeed * deltaTime;
                break;
        }
    }

    /// <summary>
    /// Applies gravity (not capped by MaxSpeed), integrates, then resets any
    /// non-finite state so a bad frame cannot poison rendering.
    /// </summary>
    public void Integrate(Vector2 gravity, float deltaTime)
    {
        Body.ApplyForce(gravity, deltaTime);
        Body.Integrate(Transform, deltaTime);

        if (!MathHelpers.IsFinite(Body.Velocity))
            Body.Velocity = Vector2.Zero;
        if (!MathHelpers.IsFinite(Transform.Position))
            Transform.Position = Vector2.Zero;
        if (!float.IsFinite(Transform.Rotation))
            Transform.Rotation = 0f;
    }
}
