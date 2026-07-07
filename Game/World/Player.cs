using Microsoft.Xna.Framework;
using Strange_Universe.Game.Entities;
using StrangeUniverse.Game.Components;
using StrangeUniverse.Input;
using System;
using System.Text.Json.Serialization;

namespace Strange_Universe.Game.World;

/// <summary>The player-controlled ship.  Pure game logic — no MonoGame rendering types.</summary>
public class Player
{
    public string Name     { get; set; } = "Player";
    public string ShipName { get; set; } = "Shuttle";
    [JsonIgnore] public ShipStats Ship  { get; set; } = new();
    [JsonIgnore] public Transform   Transform           { get; } = new();
    [JsonIgnore] public PhysicsBody Physics             { get; }
    [JsonIgnore] public string      TextureId           { get; set; } = string.Empty;
    [JsonIgnore] public float       Radius              { get; set; }
    [JsonIgnore] public float       SpriteRotationOffset => Ship.SpriteRotationOffset;

    public Player(string shipName = "Shuttle")
    {
        Ship = Ship.GetShipStats(shipName);
        Radius = Ship.Radius;
        Physics = new PhysicsBody
        {
            LinearDamping = Ship.LinearDamping,   // 1.0 = no passive drag
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
            Transform.Rotation -= Ship.RotationSpeed * deltaTime;
        if (input.RotateRight)
            Transform.Rotation += Ship.RotationSpeed * deltaTime;
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
        float maxDelta        = Ship.RotationSpeed * deltaTime;

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

        Vector2 thrustForce  = Transform.Forward * Ship.ThrustForce;
        float   currentSpeed = Physics.Velocity.Length();

        if (currentSpeed > 0f)
        {
            Vector2 velDir      = Physics.Velocity / currentSpeed;
            float   parallelMag = Vector2.Dot(thrustForce, velDir);

            // Only reduce thrust that would push speed higher (positive parallel component)
            if (parallelMag > 0f)
            {
                float softStart = Ship.MaxSpeed * Ship.SoftCapStart;

                if (currentSpeed >= Ship.MaxSpeed)
                {
                    // At or above max: strip the forward component entirely.
                    // The ship can still turn — lateral thrust is unaffected.
                    thrustForce -= velDir * parallelMag;
                }
                else if (currentSpeed > softStart)
                {
                    // Soft zone: linearly fade the forward component to zero.
                    float t = (currentSpeed - softStart) / (Ship.MaxSpeed - softStart);
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

