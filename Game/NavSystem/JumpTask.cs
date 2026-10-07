using Microsoft.Xna.Framework;
using Strange_Universe.Game.Entities;
using Strange_Universe;
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
        private readonly string _targetSystemId;
        private readonly Vector2? _originGalaxyPosition;

        public string TargetSystemId => _targetSystemId;

        /// <summary>Speed on arrival, as a multiple of the ship's normal maximum speed.</summary>
        public const float ArrivalSpeedMultiplier = 25f;

        /// <summary>Arrival speed has tapered to normal at this fraction of the system radius (0.5 = halfway to the center).</summary>
        public const float ArrivalTaperEndFraction = 0.5f;

        public JumpTask(Ship owner, Vector2? originGalaxyPosition = null, string targetSystemId = null, TaskState? currentState = null) : base(owner)
        {
            _targetSystemId = targetSystemId;
            _originGalaxyPosition = originGalaxyPosition;
            CurrentState = currentState ?? TaskState.MoveToMandeville;
            if (Owner is Player && (_targetSystemId == null || World?.SystemExists(_targetSystemId) != true))
            {
                CurrentState = TaskState.Invalid;
            }
        }

        /// <summary>When the owner has arrived in the target system, consumes the route entry and one unit of fuel.</summary>
        public override void OnCompleted()
        {
            var system = World;
            if (system == null || system.SystemId != _targetSystemId) return;

            system.RemoveFromJumpRoute(_targetSystemId);
            Owner.CurrentFuelLevel--;
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
                    Vector2 jumpDirection = MathHelpers.SafeNormalize(Owner.Position, Owner.Forward);
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
                    float systemRadius = World?.SystemRadius ?? 50000f;

                    if (distanceFromCenter > systemRadius && Owner.Velocity.LengthSquared() > Owner.ShipType.MaxSpeed * 8)
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
                        float BaseAcceleration = Owner.ShipType.ThrustForce;

                        // Exponential growth factor - increases acceleration over time
                        // The longer we accelerate, the faster we go
                        float accelerationGrowthRate = 1.5f;

                        // Calculate how far through the jump we are (0 to 1)
                        float jumpMandevilleRadius = World?.MandevilleRadius ?? 5000f;
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
                    // GOAL: Set arrival location and enter the new system
                    if (Owner is Player)
                    {
                        // Builds the new system, attaches the player and updates CurrentStarSystemID.
                        World.EnterSystem(_targetSystemId);
                    }

                    // Set ship position at system edge entry point
                    Owner.Position = World.GetSystemEdgeEntryPosition(_originGalaxyPosition);

                    // Calculate inward direction (toward system center)
                    Vector2 inwardDirection = MathHelpers.SafeNormalize(-Owner.Position, -Vector2.UnitX);

                    // Arrive at hyperspace speed, pointing inward
                    Owner.Velocity = inwardDirection * (Owner.ShipType.MaxSpeed * ArrivalSpeedMultiplier);

                    // Face the direction of travel immediately; otherwise the ship keeps its outbound
                    // heading from the jump and only turns gradually while arriving.
                    Owner.Rotation = (float)Math.Atan2(inwardDirection.Y, inwardDirection.X);

                    // Transition to the arrival deceleration phase
                    CurrentState = TaskState.ArriveInSystem;
                    break;

                case TaskState.ArriveInSystem:
                {
                    // GOAL: Enter at hyperspace speed and taper down to normal travel speed
                    // halfway between the system edge and its center.
                    float arriveRadius = World?.SystemRadius ?? 50000f;
                    float normalSpeed = Owner.ShipType.MaxSpeed;
                    float taperEnd = arriveRadius * ArrivalTaperEndFraction;
                    float arriveDistance = Owner.Position.Length();

                    Vector2 arriveInward = MathHelpers.SafeNormalize(-Owner.Position, Owner.Forward);
                    Owner.Rotation = (float)Math.Atan2(arriveInward.Y, arriveInward.X);

                    if (arriveDistance <= taperEnd)
                    {
                        // Reached the taper point: normal speed, arrival complete.
                        Owner.Velocity = arriveInward * normalSpeed;
                        CurrentState = TaskState.Complete;
                        break;
                    }

                    // Speed depends on position, not time, so it is frame-rate independent.
                    // Quadratic ease: sheds speed quickly at first, then settles onto normal speed.
                    float taperProgress = Math.Clamp((arriveRadius - arriveDistance) / (arriveRadius - taperEnd), 0f, 1f);
                    float remaining = 1f - taperProgress;
                    float arriveSpeed = normalSpeed + (normalSpeed * ArrivalSpeedMultiplier - normalSpeed) * remaining * remaining;
                    Owner.Velocity = arriveInward * arriveSpeed;
                    break;
                }

                case TaskState.Complete:
                case TaskState.Invalid:
                    // Task is complete, remain in this state
                    break;
            }
        }
    }
}
