using Microsoft.Xna.Framework;
using Strange_Universe.Game.Components;
using Strange_Universe.Game.Systems;
using StrangeUniverse;
using StrangeUniverse.Game.Components;
using System;
using System.Text.Json.Serialization;

namespace Strange_Universe.Game.Entities;

/// <summary>The player-controlled ship. Inherits shared ship functionality from Ship base class.</summary>
public class Player : Ship
{
    public string Name { get; set; } = "Player";
    public string ShipName { get; set; } = "Shuttle";
    public string CurrentStarSystemID { get; set; }
    public int? CurrentHullStrength { get; set; } = null;
    public int? CurrentShieldStrength { get; set; } = null;
    public int? CurrentFuelLevel { get; set; } = null;
    [JsonIgnore] public int MaxHullStrength { get => CalculateMaxHull(); }
    [JsonIgnore] public int MaxShieldStrength { get => CalculateMaxShield(); }
    [JsonIgnore] public int MaxFuelLevel { get => CalculateMaxFuel(); }
    [JsonIgnore] public float CurrentHullPercentage { get => (float)CurrentHullStrength / MaxHullStrength * 100f; }
    [JsonIgnore] public float CurrentShieldPercentage { get => (float)CurrentShieldStrength / MaxShieldStrength * 100f; }
    [JsonIgnore] public float CurrentFuelPercentage { get => (float)CurrentFuelLevel / MaxFuelLevel * 100f; }
    [JsonIgnore] private Camera _camera = Launcher.Camera;
    [JsonIgnore] public bool UseSafeEntryLocation { get; set; } = false;

    // ── Jump sequence ──────────────────────────────────────────────────────────────────
    [JsonIgnore] public JumpPhase JumpPhase { get; private set; } = JumpPhase.Normal;
    [JsonIgnore] private StarSystem ActiveStarSystem => Launcher.ActiveUniverse.ActiveStarSystem;

    private float _jumpAngle;
    private float _jumpAccelTime;   // seconds elapsed since the burn started; drives exponential growth

    public Player(string shipName = "Shuttle") : base(shipName)
    {
    }

    public void Update(float deltaTime, InputState input)
    {
        if (UseSafeEntryLocation)
        {
            Transform = ActiveStarSystem.GetSafeEntryTransform();
            UseSafeEntryLocation = false;
        }

        if (JumpPhase != JumpPhase.Normal)
        {
            UpdateJumpSequence(deltaTime);
        }
        else
        {
            HandleManualRotation(deltaTime, input);
            HandleRetrograde(deltaTime, input);
            HandleThrust(deltaTime, input);

            // Apply gravitational forces from celestial bodies
            // Gravity is not capped by MaxSpeed - it can push ships beyond their normal limits
            Physics.ApplyForce(ActiveStarSystem.CalculateGravityAtLocation(Transform.Position), deltaTime);
        }

        Physics.Integrate(Transform, deltaTime);
        _camera.Update(Transform.Position, deltaTime, input);
    }

    /// <summary>
    /// Begins the automated jump sequence. 
    /// </summary>
    public void BeginJump(float jumpAngle)
    {
        _jumpAngle = jumpAngle;
        JumpPhase = JumpPhase.Decelerate;
    }

    /// <summary>
    /// Begins the automated arrival sequence after jumping to a new system.
    /// Positions the player at the system edge with high velocity, automatically
    /// decelerating as they travel toward the Mandeville Point.
    /// </summary>
    public void BeginArrival(Vector2 entryPosition, Vector2 entryDirection, float mandevilleRadius)
        {
        Vector2 normalizedDirection = Vector2.Normalize(entryDirection);

        Transform.Position = entryPosition;
        Transform.Rotation = (float)Math.Atan2(entryDirection.Y, entryDirection.X);

        Physics.Velocity = normalizedDirection * ShipStats.MaxSpeed * 8f;
        JumpPhase = JumpPhase.Arrival;
    }

    private void UpdateJumpSequence(float deltaTime)
    {
        switch (JumpPhase)
        {
            // ── Phase 1: spin to face retrograde and brake to a halt ──────────
            case JumpPhase.Decelerate:
                {
                    if (Physics.Velocity.LengthSquared() < 100f)
                    {
                        Physics.Velocity = Vector2.Zero;
                        JumpPhase = JumpPhase.Align;
                        break;
                    }

                    float retroAngle = (float)Math.Atan2(-Physics.Velocity.Y, -Physics.Velocity.X);
                    RotateTowards(retroAngle, deltaTime);

                    // Start braking once reasonably aligned with retrograde
                    if (Math.Abs(StaticHelpers.WrapAngle(retroAngle - Transform.Rotation)) <= 0.3f)
                        Physics.ApplyForce(Transform.Forward * ShipStats.ThrustForce, deltaTime);
                    break;
                }

            // ── Phase 2: rotate to face destination ───────────────────────────
            case JumpPhase.Align:
                {
                    if (RotateTowards(_jumpAngle, deltaTime, 0.04f))
                    {
                        Transform.Rotation = _jumpAngle;
                        _jumpAccelTime = 0f;
                        JumpPhase = JumpPhase.Accelerate;
                    }
                    break;
                }

            // ── Phase 3: full-throttle burn — exponentially growing, no speed cap ─────
            case JumpPhase.Accelerate:
                {
                    _jumpAccelTime += deltaTime;
                    // Force doubles roughly every 0.5 s (e^(1.4*0.5) ≈ 2)
                    float force = ShipStats.ThrustForce * 8f * (float)Math.Exp(1.4f * _jumpAccelTime);
                    Physics.ApplyForce(Transform.Forward * force, deltaTime);

                    // Phase continues until Universe detects we've left the system and calls JumpToSystem
                    break;
                }

            // ── Phase 4: arrival — automatic deceleration from system edge to Mandeville Point ─────
            case JumpPhase.Arrival:
                {
                    float distanceFromCenter = Transform.Position.Length();

                    // Check if we've reached the Mandeville Point
                    if (distanceFromCenter <= ActiveStarSystem.MandevilleRadius)
                    {
                        JumpPhase = JumpPhase.Normal;
                        Physics.Velocity = Vector2.Normalize(Physics.Velocity) * ShipStats.MaxSpeed;
                        return;
                    }

                    // Calculate and apply interpolated speed
                    float progress = Math.Clamp((ActiveStarSystem.SystemRadius - distanceFromCenter) / (ActiveStarSystem.SystemRadius - ActiveStarSystem.MandevilleRadius), 0f, 1f);
                    Physics.Velocity = Vector2.Normalize(Physics.Velocity) * MathHelper.Lerp(ShipStats.MaxSpeed * 8f, ShipStats.MaxSpeed, progress);
                    break;
                }
        }
    }
    private int CalculateMaxHull()
    {
        return ShipStats.MaxHull;
    }

    private int CalculateMaxShield()
    {
        return ShipStats.MaxShield;
    }

    private int CalculateMaxFuel()
    {
        return ShipStats.MaxFuel;
    }

    // ── Rotation ─────────────────────────────────────────────────────────────
    // A/D rotate the ship facing immediately and responsively.
    // Facing is independent of the current velocity vector.

    private void HandleManualRotation(float deltaTime, InputState input)
    {
        if (input.RotateLeft)
            Transform.Rotation -= ShipStats.RotationSpeed * deltaTime;
        if (input.RotateRight)
            Transform.Rotation += ShipStats.RotationSpeed * deltaTime;
    }

    // ── Maneuvering thrusters (S) ─────────────────────────────────────────────
    // Rotates the ship to face directly opposite the current velocity vector
    // so that W thrust will decelerate the ship.  A/D can combine with this.

    private void HandleRetrograde(float deltaTime, InputState input)
    {
        if (!input.Retrograde || Physics.Velocity.LengthSquared() < 1f)
            return;

        RotateTowards((float)Math.Atan2(-Physics.Velocity.Y, -Physics.Velocity.X), deltaTime);
    }

    // ── Forward thrust (W) ────────────────────────────────────────────────────
    // Adds acceleration in the current facing direction using base Ship.ApplyThrust.

    private void HandleThrust(float deltaTime, InputState input)
    {
        if (!input.Thrust) return;
        ApplyThrust(deltaTime);
    }

    // ── Helper Methods ────────────────────────────────────────────────────────────
    // Uses base RotateTowards method from Ship class
}

