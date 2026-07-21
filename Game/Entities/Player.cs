using Microsoft.Xna.Framework;
using Strange_Universe.Game.Components;
using Strange_Universe.Game.Systems;
using StrangeUniverse;
using StrangeUniverse.Game.Components;
using System;
using System.Text.Json.Serialization;

namespace Strange_Universe.Game.Entities;

/// <summary>The player-controlled ship.  Pure game logic — no MonoGame rendering types.</summary>
public class Player
{
    public string Name     { get; set; } = "Player";
    public string ShipName { get; set; } = "Shuttle";
    public string CurrentStarSystemID { get; set; }
    [JsonIgnore] public ShipStats Ship  { get; set; } = new();
    public Transform   Transform           { get; set; } = new();
    [JsonIgnore] public PhysicsBody Physics             { get; }
    [JsonIgnore] public string      TextureId           { get; set; } = string.Empty;
    [JsonIgnore] public float       Radius              { get; set; }
    [JsonIgnore] public float       SpriteRotationOffset => Ship.SpriteRotationOffset;

    // ── Jump sequence ──────────────────────────────────────────────────────────────────
    [JsonIgnore] public JumpPhase JumpPhase            { get; private set; } = JumpPhase.None;
    [JsonIgnore] public bool      IsJumping            => JumpPhase != JumpPhase.None;
    [JsonIgnore] public bool      JumpSequenceComplete { get; set; }

    private float _jumpAngle;
    private float _jumpSystemRadius;
    private float _jumpAccelTime;   // seconds elapsed since the burn started; drives exponential growth

    private const float JumpAccelMultiplier = 5f;   // multiplier on ThrustForce during the jump burn
    private const float StopSpeedThreshold  = 10f;  // world units/s LengthSquared — treated as "stopped"
    private const float BrakeAlignThreshold = 0.3f; // radians — begin braking once within this of retrograde
    private const float JumpAlignThreshold  = 0.04f;// radians — snap to jump heading once within this

    public Player(string shipName = "Shuttle")
    {
        //const string id = "player_ship";
        Ship = Ship.GetShipStats(shipName);
        Radius = Ship.Radius;
        Physics = new PhysicsBody
        {
            LinearDamping = Ship.LinearDamping,   // 1.0 = no passive drag
            Mass          = 1f,
        };
    }

    public void Generate()
    {
        var tex = ArtLoader.TryLoad(Launcher.GD, Ship.SpriteName);
        Launcher.TextureCache.Register(Ship.ShipName, tex);
    }

    public void Update(float deltaTime, InputState input)
    {
        if (IsJumping)
            UpdateJumpSequence(deltaTime);
        else
        {
            HandleManualRotation(deltaTime, input);
            HandleRetrograde(deltaTime, input);
            HandleThrust(deltaTime, input);
        }
        Physics.Integrate(Transform, deltaTime);
    }

    /// <summary>
    /// Begins the automated jump sequence.  If the ship already has significant velocity
    /// it decelerates first; otherwise it skips straight to the alignment phase.
    /// </summary>
    public void BeginJump(float jumpAngle, float systemRadius)
    {
        _jumpAngle           = jumpAngle;
        _jumpSystemRadius    = systemRadius;
        JumpSequenceComplete = false;
        JumpPhase            = Physics.Velocity.LengthSquared() < StopSpeedThreshold * StopSpeedThreshold
            ? JumpPhase.Align
            : JumpPhase.Decelerate;
    }

    private void UpdateJumpSequence(float deltaTime)
    {
        switch (JumpPhase)
        {
            // ── Phase 1: spin to face retrograde and brake to a halt ──────────
            case JumpPhase.Decelerate:
            {
                if (Physics.Velocity.LengthSquared() < StopSpeedThreshold * StopSpeedThreshold)
                {
                    Physics.Velocity = Vector2.Zero;
                    JumpPhase        = JumpPhase.Align;
                    break;
                }

                float retroAngle = (float)Math.Atan2(-Physics.Velocity.Y, -Physics.Velocity.X);
                float diff       = StaticHelpers.WrapAngle(retroAngle - Transform.Rotation);
                float maxDelta   = Ship.RotationSpeed * deltaTime;

                if (Math.Abs(diff) <= maxDelta)
                    Transform.Rotation = retroAngle;
                else
                    Transform.Rotation += Math.Sign(diff) * maxDelta;

                // Start braking once reasonably aligned with retrograde
                if (Math.Abs(StaticHelpers.WrapAngle(retroAngle - Transform.Rotation)) <= BrakeAlignThreshold)
                    Physics.ApplyForce(Transform.Forward * Ship.ThrustForce, deltaTime);
                break;
            }

            // ── Phase 2: rotate to face destination ───────────────────────────
            case JumpPhase.Align:
            {
                float diff     = StaticHelpers.WrapAngle(_jumpAngle - Transform.Rotation);
                float maxDelta = Ship.RotationSpeed * deltaTime;

                if (Math.Abs(diff) <= JumpAlignThreshold)
                {
                    Transform.Rotation = _jumpAngle;
                    _jumpAccelTime     = 0f;
                    JumpPhase          = JumpPhase.Accelerate;
                }
                else
                    Transform.Rotation += Math.Sign(diff) * maxDelta;
                break;
            }

            // ── Phase 3: full-throttle burn — exponentially growing, no speed cap ─────
            case JumpPhase.Accelerate:
            {
                _jumpAccelTime += deltaTime;
                // Force doubles roughly every 0.5 s (e^(1.4*0.5) ≈ 2)
                float force = Ship.ThrustForce * JumpAccelMultiplier
                              * (float)Math.Exp(1.4f * _jumpAccelTime);
                Physics.ApplyForce(Transform.Forward * force, deltaTime);

                if (Transform.Position.LengthSquared() >= _jumpSystemRadius * _jumpSystemRadius)
                {
                    JumpPhase            = JumpPhase.None;
                    JumpSequenceComplete = true;
                }
                break;
            }
        }
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
        float diff            = StaticHelpers.WrapAngle(retrogradeAngle - Transform.Rotation);
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
}

