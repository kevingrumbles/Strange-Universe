using System;
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
            LinearDamping = stats.LinearDamping,   // 1.0 = no passive drag
            Mass          = 1f,
        };
    }

    public void Update(float deltaTime, InputState input)
    {
        HandleManualRotation(deltaTime, input);
        HandleRetrograde(deltaTime, input);
        HandleThrust(deltaTime, input);
        Physics.Integrate(Transform, deltaTime);
    }

    // ── Rotation ─────────────────────────────────────────────────────────────
    // A/D rotate the ship facing immediately and responsively.
    // Facing is independent of the current velocity vector.

    private void HandleManualRotation(float deltaTime, InputState input)
    {
        if (input.RotateLeft)
            Transform.Rotation -= _stats.RotationSpeed * deltaTime;
        if (input.RotateRight)
            Transform.Rotation += _stats.RotationSpeed * deltaTime;
    }

    // ── Maneuvering thrusters (S) ─────────────────────────────────────────────
    // Rotates the ship to face directly opposite the current velocity vector
    // so that W thrust will decelerate the ship.  A/D can combine with this.

    private void HandleRetrograde(float deltaTime, InputState input)
    {
        if (!input.Retrograde || Physics.Velocity.LengthSquared() < 1f)
            return;

        float retrogradeAngle = (float)Math.Atan2(-Physics.Velocity.Y, -Physics.Velocity.X);
        float diff            = WrapAngle(retrogradeAngle - Transform.Rotation);
        float maxDelta        = _stats.RotationSpeed * deltaTime;

        if (Math.Abs(diff) <= maxDelta)
            Transform.Rotation = retrogradeAngle;   // snap when very close
        else
            Transform.Rotation += Math.Sign(diff) * maxDelta;
    }

    // ── Forward thrust (W) ────────────────────────────────────────────────────
    // Adds acceleration in the current facing direction.
    // Momentum is additive — thrust never redirects existing velocity instantly.
    //
    // Soft speed cap: as speed approaches MaxSpeed the thrust component that
    // would increase speed is gradually reduced.  Lateral/decelerating components
    // are never reduced, so turns and braking still feel responsive at top speed.

    private void HandleThrust(float deltaTime, InputState input)
    {
        if (!input.Thrust) return;

        Vector2 thrustForce  = Transform.Forward * _stats.ThrustForce;
        float   currentSpeed = Physics.Velocity.Length();

        if (currentSpeed > 0f)
        {
            Vector2 velDir      = Physics.Velocity / currentSpeed;
            float   parallelMag = Vector2.Dot(thrustForce, velDir);

            // Only reduce thrust that would push speed higher (positive parallel component)
            if (parallelMag > 0f)
            {
                float softStart = _stats.MaxSpeed * _stats.SoftCapStart;

                if (currentSpeed >= _stats.MaxSpeed)
                {
                    // At or above max: strip the forward component entirely.
                    // The ship can still turn — lateral thrust is unaffected.
                    thrustForce -= velDir * parallelMag;
                }
                else if (currentSpeed > softStart)
                {
                    // Soft zone: linearly fade the forward component to zero.
                    float t = (currentSpeed - softStart) / (_stats.MaxSpeed - softStart);
                    thrustForce -= velDir * (parallelMag * t);
                }
                // Below softStart: full thrust, no reduction
            }
            // Negative parallel (decelerating) and lateral components: never reduced
        }

        Physics.ApplyForce(thrustForce, deltaTime);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Wraps an angle to the range [−π, π] for the shortest-path rotation calc.</summary>
    private static float WrapAngle(float angle)
    {
        angle %= MathHelper.TwoPi;
        if (angle >  MathHelper.Pi) angle -= MathHelper.TwoPi;
        if (angle < -MathHelper.Pi) angle += MathHelper.TwoPi;
        return angle;
    }
}

