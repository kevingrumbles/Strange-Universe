using Microsoft.Xna.Framework;
using Strange_Universe.Game.Entities;
using StrangeUniverse;
using System;
using System.Linq;

namespace Strange_Universe.Game.NavSystem
{
    /// <summary>
    /// Handles automated jump navigation from current system to target system.
    /// Uses Ship's RotateTowards and ApplyThrust methods to control navigation through each state.
    /// </summary>
    public class JumpTask : NavTask
    {
        public readonly string _targetSystemId;
        private readonly Vector2 _originGalaxyPosition;

        public JumpTask(Ship owner, string targetSystemId) : base(owner)
        {
            _targetSystemId = targetSystemId;
            _originGalaxyPosition = owner.StarSystem.GalaxyPosition;
            CurrentState = TaskState.MoveToMandeville;
        }

        public override void Update(float deltaTime)
        {
            if (Owner == null) return;

            switch (CurrentState)
            {
                case TaskState.MoveToMandeville:
                    // GOAL: Move away from system center until outside Mandeville radius and clear of gravity
                    if (Owner.CanJump())
                    {
                        // Successfully reached jump-capable position
                        CurrentState = TaskState.Decelerate;
                    }
                    else
                    {
                        // Calculate outbound direction (away from system center)
                        Vector2 outboundDirection = Owner.Position - Vector2.Zero;

                        if (outboundDirection.LengthSquared() > 0.1f)
                        {
                            // Calculate target rotation angle
                            float targetAngle = (float)Math.Atan2(outboundDirection.Y, outboundDirection.X);

                            // Rotate toward outbound direction
                            const float alignmentThreshold = 0.2f; // ~11 degrees
                            bool isAligned = Owner.RotateTowards(targetAngle, deltaTime, alignmentThreshold);

                            // Once aligned, apply thrust to accelerate outward
                            if (isAligned)
                            {
                                Owner.ApplyThrust(deltaTime);
                            }
                        }
                        else
                        {
                            // At system center, pick arbitrary outbound direction and thrust
                            Owner.RotateTowards(0f, deltaTime); // Face right
                            Owner.ApplyThrust(deltaTime);
                        }
                    }
                    break;

                case TaskState.Decelerate:
                    // GOAL: Slow down to a safe jump speed
                    const float SafeJumpSpeed = 50f;

                    if (Owner.Speed <= SafeJumpSpeed)
                    {
                        // Reached safe speed
                        CurrentState = TaskState.AlignForJump;
                    }
                    else
                    {
                        // Calculate retrograde angle (opposite of velocity)
                        if (Owner.Velocity.LengthSquared() > 1f)
                        {
                            float retrogradeAngle = (float)Math.Atan2(-Owner.Velocity.Y, -Owner.Velocity.X);

                            // Rotate toward retrograde
                            const float brakeAlignmentThreshold = 0.2f;
                            bool isFacingRetrograde = Owner.RotateTowards(retrogradeAngle, deltaTime, brakeAlignmentThreshold);

                            // Apply thrust retrograde to slow down
                            if (isFacingRetrograde)
                            {
                                Owner.ApplyThrust(deltaTime);
                            }
                        }
                    }
                    break;

                case TaskState.AlignForJump:
                    // GOAL: Align ship for jump (pointing away from system center)
                    Vector2 jumpDirection = Vector2.Normalize(Owner.Position);
                    float jumpAngle = (float)Math.Atan2(jumpDirection.Y, jumpDirection.X);

                    // Rotate toward jump angle with tighter alignment tolerance
                    const float jumpAlignmentThreshold = 0.05f; // ~3 degrees
                    bool isAlignedForJump = Owner.RotateTowards(jumpAngle, deltaTime, jumpAlignmentThreshold);

                    if (isAlignedForJump)
                    {
                        CurrentState = TaskState.Jump;
                    }
                    break;

                case TaskState.Jump:
                    // GOAL: Accelerate with exponential acceleration past normal limits until beyond system edge
                    float distanceFromCenter = Owner.Position.Length();
                    float systemRadius = Owner.StarSystem?.SystemRadius ?? 50000f;

                    if (distanceFromCenter > systemRadius && Owner.Velocity.LengthSquared() > Owner.ShipStats.MaxSpeed * 8)
                    {
                        // Successfully jumped beyond system edge
                        if (Owner is Player) 
                        {
                            CurrentState = TaskState.SystemTranslation;
                        }
                        else if (Owner is Nonplayer)
                        {
                            ((Nonplayer)Owner).Remove = true;
                            CurrentState = TaskState.Complete;
                        }
                    }
                    else
                    {
                        // Apply exponential acceleration - bypasses normal MaxSpeed limits
                        // Base acceleration rate (units per second per second)
                        float BaseAcceleration = Owner.ShipStats.ThrustForce;

                        // Exponential growth factor - increases acceleration over time
                        // The longer we accelerate, the faster we go
                        float accelerationGrowthRate = 1.5f;

                        // Calculate how far through the jump we are (0 to 1)
                        float jumpMandevilleRadius = Owner.StarSystem?.MandevilleRadius ?? 5000f;
                        float jumpProgress = Math.Max(0f, (distanceFromCenter - jumpMandevilleRadius) / (systemRadius - jumpMandevilleRadius));

                        // Exponential acceleration: starts at BaseAcceleration, grows exponentially
                        float currentAcceleration = BaseAcceleration * (float)Math.Pow(accelerationGrowthRate, jumpProgress * 10f);

                        // Apply acceleration in the direction of current velocity (continuing jump trajectory)
                        if (Owner.Velocity.LengthSquared() > 1f)
                        {
                            Vector2 velocityDirection = Vector2.Normalize(Owner.Velocity);

                            // Directly modify velocity to bypass MaxSpeed soft cap
                            // This simulates the ship's jump drive overriding normal thrust limits
                            Owner.Velocity += velocityDirection * currentAcceleration * deltaTime;
                        }
                        else
                        {
                            // If somehow velocity was lost, accelerate in facing direction
                            Owner.Velocity += Owner.Forward * currentAcceleration * deltaTime;
                        }
                    }
                    break;

                case TaskState.SystemTranslation:
                    // GOAL: Set arrival location and generate new system
                    // Update the player's current system ID so the StarSystem reference is correct
                    ((Player)Owner).CurrentStarSystemID = _targetSystemId;
                    Launcher.ActiveUniverse.Generate(); 

                    // Set ship position at system edge entry point
                    Owner.Position = Owner.StarSystem.GetSystemEdgeEntryPosition(_originGalaxyPosition);

                    // Calculate inward direction (toward system center)
                    Vector2 inwardDirection = Vector2.Normalize(-Owner.Position);

                    // Keep the high velocity from the Jump state, but ensure it's pointing inward
                    Owner.Velocity = inwardDirection * Owner.Speed;

                    // Transition to the arrival deceleration phase
                    CurrentState = TaskState.ArriveInSystem;
                    break;

                case TaskState.ArriveInSystem:
                    // GOAL: Decelerate from hyperspace speed to normal speed at the Mandeville point

                    // Calculate target position at Mandeville radius (where we want to arrive)
                    float mandevilleRadius = Owner.StarSystem?.MandevilleRadius ?? 5000f;
                    float currentDistanceFromCenter = Owner.Position.Length();

                    // Check if we're already inside the Mandeville radius (might have overshot)
                    if (currentDistanceFromCenter < mandevilleRadius)
                    {
                        // Already inside Mandeville radius - complete the jump
                        Vector2 directionToCenter = Vector2.Normalize(-Owner.Position);
                        Owner.Velocity = directionToCenter * Owner.ShipStats.MaxSpeed;
                        float inwardRotation = (float)Math.Atan2(directionToCenter.Y, directionToCenter.X);
                        Owner.Rotation = inwardRotation;
                        CurrentState = TaskState.Complete;
                        break;
                    }

                    // Target is on the same line toward center, at Mandeville distance
                    Vector2 directionToCenter2 = Vector2.Normalize(-Owner.Position);
                    Vector2 targetPosition = directionToCenter2 * mandevilleRadius;

                    // Calculate distance to target (Mandeville point)
                    float distanceToTarget = Vector2.Distance(Owner.Position, targetPosition);
                    float currentSpeed = Owner.Speed;
                    float targetSpeed = Owner.ShipStats.MaxSpeed;

                    // Check if we've arrived (close to Mandeville point and at reasonable speed)
                    const float ArrivalDistanceThreshold = 1000f; // Within 1000 units of Mandeville point
                    if (distanceToTarget < ArrivalDistanceThreshold && currentSpeed <= targetSpeed * 1.5f)
                    {
                        // Arrived successfully - snap to target position and complete
                        Owner.Position = targetPosition;
                        float inwardRotation = (float)Math.Atan2(directionToCenter2.Y, directionToCenter2.X);
                        Owner.Rotation = inwardRotation;
                        Owner.Velocity = directionToCenter2 * targetSpeed;
                        CurrentState = TaskState.Complete;
                    }
                    else
                    {
                        // Apply exponential deceleration while maintaining inward direction
                        // Calculate deceleration progress (0 = at system edge, 1 = at Mandeville)
                        float systemRad = Owner.StarSystem?.SystemRadius ?? 50000f;
                        float totalDecelerationDistance = systemRad - mandevilleRadius;
                        float distanceTraveled = systemRad - currentDistanceFromCenter;
                        float decelerationProgress = Math.Clamp(distanceTraveled / totalDecelerationDistance, 0f, 1f);

                        // Exponential deceleration: stronger as we get closer
                        float decelerationRate = 1.8f; // Growth rate for deceleration
                        float decelerationStrength = (float)Math.Pow(decelerationRate, decelerationProgress * 10f);

                        // Base deceleration force
                        const float BaseDeceleration = 800f;
                        float currentDeceleration = BaseDeceleration * decelerationStrength;

                        // Calculate desired speed at this distance to arrive at Mandeville with target speed
                        // Use a simple linear interpolation as a guide
                        float distanceRatio = distanceToTarget / totalDecelerationDistance;
                        float desiredSpeed = targetSpeed + (currentSpeed - targetSpeed) * distanceRatio;
                        desiredSpeed = Math.Max(targetSpeed, desiredSpeed);

                        // Apply deceleration (reduce velocity magnitude)
                        if (currentSpeed > desiredSpeed)
                        {
                            // Slow down exponentially
                            Vector2 velocityDirection = Vector2.Normalize(Owner.Velocity);
                            float speedReduction = currentDeceleration * deltaTime;

                            // Don't overshoot - clamp to desired speed
                            float newSpeed = Math.Max(desiredSpeed, currentSpeed - speedReduction);
                            Owner.Velocity = velocityDirection * newSpeed;
                        }

                        // Ensure velocity is pointing toward center
                        Vector2 currentVelocityDir = Vector2.Normalize(Owner.Velocity);
                        float directionAlignment = Vector2.Dot(currentVelocityDir, directionToCenter2);

                        // If not pointing inward, adjust velocity direction
                        if (directionAlignment < 0.95f)
                        {
                            Owner.Velocity = directionToCenter2 * Owner.Speed;
                        }

                        // Rotate to face inward (toward system center) as we approach
                        float inwardAngle = (float)Math.Atan2(directionToCenter2.Y, directionToCenter2.X);
                        Owner.RotateTowards(inwardAngle, deltaTime, 0.1f);
                    }
                    break;

                case TaskState.Complete:
                    // Task is complete, remain in this state
                    break;
            }
        }
    }
}
