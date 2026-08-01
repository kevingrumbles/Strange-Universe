using Microsoft.Xna.Framework;
using Strange_Universe.Game.Components;
using Strange_Universe.Game.Systems;
using StrangeUniverse;
using StrangeUniverse.Game.Components;
using System;
using System.Text.Json.Serialization;

namespace Strange_Universe.Game.Entities;

/// <summary>
/// Base class for all ships (player and NPCs).
/// Contains shared functionality: movement, physics, rendering, and ship statistics.
/// Does not contain player input or AI logic.
/// </summary>
public abstract class Ship
{
    [JsonIgnore] public ShipStats ShipStats { get; set; } = new();
    public StrangeUniverse.Game.Components.Transform Transform { get; set; } = new();
    [JsonIgnore] public StrangeUniverse.Game.Components.PhysicsBody Physics { get; protected set; }
    [JsonIgnore] public float Radius { get; set; }

    protected Ship(string shipName = "Shuttle")
    {
        ShipStats = ShipStats.GetShipStats(shipName);
        Radius = ShipStats.Radius;
        Physics = new StrangeUniverse.Game.Components.PhysicsBody
        {
            Mass = 1f,
        };
    }

    /// <summary>
    /// Loads the ship's sprite texture and registers it in the texture cache.
    /// </summary>
    public virtual void Generate()
    {
        var tex = Strange_Universe.Game.Systems.ArtLoader.TryLoad(Launcher.GD, ShipStats.SpriteName);
        Launcher.TextureCache.Register(ShipStats.ShipName, tex);
    }

    /// <summary>
    /// Applies thrust force in the ship's current facing direction.
    /// Uses a soft speed cap that gradually reduces acceleration-contributing thrust as MaxSpeed is approached.
    /// Lateral and decelerating components are never reduced.
    /// </summary>
    protected void ApplyThrust(float deltaTime)
    {
        Vector2 thrustForce = Transform.Forward * ShipStats.ThrustForce;
        float currentSpeed = Physics.Velocity.Length();

        if (currentSpeed > 0f)
        {
            Vector2 velDir = Physics.Velocity / currentSpeed;
            float parallelMag = Vector2.Dot(thrustForce, velDir);

            // Only reduce thrust that would push speed higher (positive parallel component)
            if (parallelMag > 0f)
            {
                float softStart = ShipStats.MaxSpeed * ShipStats.SoftCapStart;

                if (currentSpeed >= ShipStats.MaxSpeed)
                {
                    // At or above max: strip the forward component entirely.
                    // The ship can still turn — lateral thrust is unaffected.
                    thrustForce -= velDir * parallelMag;
                }
                else if (currentSpeed > softStart)
                {
                    // Soft zone: linearly fade the forward component to zero.
                    float t = (currentSpeed - softStart) / (ShipStats.MaxSpeed - softStart);
                    thrustForce -= velDir * (parallelMag * t);
                }
                // Below softStart: full thrust, no reduction
            }
            // Negative parallel (decelerating) and lateral components: never reduced
        }

        Physics.ApplyForce(thrustForce, deltaTime);
    }

    /// <summary>
    /// Rotates the ship towards a target angle.
    /// Returns true if already aligned within the threshold.
    /// </summary>
    protected bool RotateTowards(float targetAngle, float deltaTime, float threshold = float.MaxValue)
    {
        float diff = StaticHelpers.WrapAngle(targetAngle - Transform.Rotation);
        float maxDelta = ShipStats.RotationSpeed * deltaTime;

        if (Math.Abs(diff) <= Math.Min(maxDelta, threshold))
        {
            Transform.Rotation = targetAngle;
            return true;
        }

        Transform.Rotation += Math.Sign(diff) * maxDelta;
        return false;
    }

    /// <summary>
    /// Applies rotation input to the ship.
    /// </summary>
    protected void ApplyRotation(float rotationInput, float deltaTime)
    {
        Transform.Rotation += rotationInput * ShipStats.RotationSpeed * deltaTime;
    }

    /// <summary>
    /// Applies gravitational forces from the star system and integrates physics.
    /// </summary>
    protected void UpdatePhysics(float deltaTime, StarSystem system)
    {
        // Apply gravitational forces from celestial bodies
        // Gravity is not capped by MaxSpeed - it can push ships beyond their normal limits
        Physics.ApplyForce(system.CalculateGravityAtLocation(Transform.Position), deltaTime);
        Physics.Integrate(Transform, deltaTime);
    }
}
