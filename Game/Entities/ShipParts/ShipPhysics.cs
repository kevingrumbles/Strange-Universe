using System;
using Microsoft.Xna.Framework;
using Strange_Universe.Game.Components;
using Strange_Universe.Game.Components.ShipComponents;

namespace Strange_Universe.Game.Entities.ShipParts;

/// <summary>
/// Ship movement: soft-capped thrust, rotation, gravity application and integration. It moves the <see cref="WorldBody"/> it is given (the ship).
/// </summary>
public class ShipPhysics
{
    private readonly WorldBody _body;
    private readonly Func<FlightStats> _stats;

    public ShipPhysics(WorldBody body, Func<FlightStats> stats)
    {
        _body = body ?? throw new ArgumentNullException(nameof(body));
        _stats = stats ?? throw new ArgumentNullException(nameof(stats));
    }

    private FlightStats Stats => _stats();

    /// <summary>
    /// Applies thrust force in the ship's current facing direction.
    /// Uses a soft speed cap that gradually reduces acceleration-contributing thrust as MaxSpeed is approached.
    /// Lateral and decelerating components are never reduced.
    /// </summary>
    public void ApplyThrust(float deltaTime)
    {
        FlightStats stats = Stats;
        Vector2 thrustForce = _body.Forward * stats.ThrustForce;
        Vector2 velocity = _body.Velocity;
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

        _body.ApplyForce(thrustForce, deltaTime);
    }

    /// <summary>
    /// Rotates towards a target angle. Returns true if already aligned within the threshold.
    /// </summary>
    public bool RotateTowards(float targetAngle, float deltaTime, float threshold = float.MaxValue)
    {
        if (float.IsNaN(targetAngle) || float.IsInfinity(targetAngle)) return false;
        if (float.IsNaN(_body.Rotation) || float.IsInfinity(_body.Rotation))
            _body.Rotation = 0f;

        float diff = MathHelpers.WrapAngle(targetAngle - _body.Rotation);
        float maxDelta = Stats.RotationSpeed * deltaTime;

        if (Math.Abs(diff) <= Math.Min(maxDelta, threshold))
        {
            _body.Rotation = targetAngle;
            return true;
        }

        _body.Rotation += Math.Sign(diff) * maxDelta;
        return false;
    }

    /// <summary>Applies manual rotation input.</summary>
    public void ApplyRotation(Direction d, float deltaTime)
    {
        switch (d)
        {
            case Direction.Left:
                _body.Rotation -= Stats.RotationSpeed * deltaTime;
                break;
            case Direction.Right:
                _body.Rotation += Stats.RotationSpeed * deltaTime;
                break;
        }
    }

    /// <summary>
    /// Applies gravity (not capped by MaxSpeed), integrates, then resets any
    /// non-finite state so a bad frame cannot poison rendering.
    /// </summary>
    public void Integrate(Vector2 gravity, float deltaTime)
    {
        _body.ApplyForce(gravity, deltaTime);
        _body.Integrate(deltaTime);

        if (!MathHelpers.IsFinite(_body.Velocity))
            _body.Velocity = Vector2.Zero;
        if (!MathHelpers.IsFinite(_body.Position))
            _body.Position = Vector2.Zero;
        if (!float.IsFinite(_body.Rotation))
            _body.Rotation = 0f;
    }
}
