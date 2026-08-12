using Microsoft.Xna.Framework;
using Strange_Universe.Game.Entities;
using StrangeUniverse;
using System;
using System.Linq;

namespace Strange_Universe.Game.NavSystem
{
    /// <summary>
    /// Handles docking at a specific location by approaching, holding position, and completing after a delay.
    /// Uses Ship's RotateTowards and ApplyThrust methods to control navigation.
    /// </summary>
    public class DockTask : NavTask
    {
        private Vector2 _target;
        private Vector2 _holdPosition;
        private const float SafeDistance = 500f; // Safe distance from target
        private const float ApproachThreshold = 600f; // When to transition to hold position
        private float _holdTimer = 0f;
        private float _holdDuration;

        public DockTask(Ship owner, Vector2 target) : base(owner)
        {
            _target = target;
            CurrentState = TaskState.ApproachPosition;

            // Random hold duration between 15 and 60 seconds
            Random rand = new Random();
            _holdDuration = 15f + (float)(rand.NextDouble() * 45f);
        }

        public override void Update(float deltaTime)
        {
            if (Owner == null) return;

            switch (CurrentState)
            {
                case TaskState.ApproachPosition:
                    // APPROACH PHASE: Move toward dock position, slowing down near the end
                    float distanceFromTarget = Vector2.Distance(Owner.Position, _target);

                    // Check if we're close enough to start holding
                    if (distanceFromTarget <= ApproachThreshold)
                    {
                        // Calculate a safe hold position
                        Vector2 directionFromTarget = Owner.Position - _target;
                        if (directionFromTarget.LengthSquared() < 0.1f)
                        {
                            // If at exact target position, pick a direction
                            Random rand = new Random();
                            float randomAngle = (float)(rand.NextDouble() * Math.PI * 2);
                            directionFromTarget = new Vector2((float)Math.Cos(randomAngle), (float)Math.Sin(randomAngle));
                        }
                        else
                        {
                            directionFromTarget = Vector2.Normalize(directionFromTarget);
                        }

                        _holdPosition = _target + directionFromTarget * SafeDistance;
                        CurrentState = TaskState.HoldPosition;
                        _holdTimer = 0f;
                        break;
                    }

                    Vector2 directionToTarget = _target - Owner.Position;

                    if (directionToTarget.LengthSquared() > 0.1f)
                    {
                        directionToTarget.Normalize();

                        // Get current gravity force
                        Vector2 approachGravity = Owner.CurrentGravity;

                        // Calculate desired speed based on distance
                        const float DecelerationDistance = 1500f;
                        float speedFactor = 1.0f;

                        if (distanceFromTarget < DecelerationDistance)
                        {
                            // Slow down as we get closer
                            speedFactor = Math.Max(0.3f, distanceFromTarget / DecelerationDistance);
                        }

                        float desiredSpeed = Owner.ShipStats.MaxSpeed * speedFactor;

                        // Check if gravity is pushing us toward target (need extra braking)
                        bool gravityAcceleratingTowardTarget = false;
                        if (approachGravity.LengthSquared() > 0.1f)
                        {
                            float gravityAlignment = Vector2.Dot(Vector2.Normalize(approachGravity), directionToTarget);
                            gravityAcceleratingTowardTarget = gravityAlignment > 0.3f;
                        }

                        // Decide whether to thrust forward or brake
                        bool needsBraking = Owner.Speed > desiredSpeed || 
                                           (gravityAcceleratingTowardTarget && distanceFromTarget < DecelerationDistance);

                        if (needsBraking)
                        {
                            // Brake: point retrograde and thrust
                            if (Owner.Velocity.LengthSquared() > 0.1f)
                            {
                                Vector2 retrogradeDir = -Vector2.Normalize(Owner.Velocity);
                                float retrogradeAngle = (float)Math.Atan2(retrogradeDir.Y, retrogradeDir.X);
                                bool isBrakeAligned = Owner.RotateTowards(retrogradeAngle, deltaTime, 0.2f);

                                if (isBrakeAligned)
                                {
                                    Owner.ApplyThrust(deltaTime);
                                }
                            }
                        }
                        else
                        {
                            // Accelerate toward target
                            float targetAngle = (float)Math.Atan2(directionToTarget.Y, directionToTarget.X);
                            bool isAligned = Owner.RotateTowards(targetAngle, deltaTime, 0.15f);

                            if (isAligned)
                            {
                                Owner.ApplyThrust(deltaTime);
                            }
                        }
                    }
                    break;

                case TaskState.HoldPosition:
                    // HOLD PHASE: Maintain position near target

                    // Increment hold timer
                    _holdTimer += deltaTime;

                    // Check if hold duration is complete
                    if (_holdTimer >= _holdDuration)
                    {
                        CurrentState = TaskState.Complete;
                        break;
                    }

                    // Get current gravity force
                    Vector2 gravityForce = Owner.CurrentGravity;

                    // Station-keeping: maintain position at hold point
                    Vector2 toHoldPosition = _holdPosition - Owner.Position;
                    float distanceFromHoldPosition = toHoldPosition.Length();

                    // Calculate total correction force needed (position correction + gravity compensation)
                    Vector2 desiredDirection = Vector2.Zero;
                    bool needsCorrection = false;

                    // If drifting too far from hold position, correct
                    const float HoldPositionTolerance = 100f;
                    if (distanceFromHoldPosition > HoldPositionTolerance)
                    {
                        toHoldPosition.Normalize();
                        desiredDirection = toHoldPosition;
                        needsCorrection = true;
                    }
                    else if (Owner.Speed > Owner.ShipStats.MaxSpeed * 0.1f)
                    {
                        // Slow down if moving too fast while in position
                        Vector2 velocityDir = Owner.Velocity;
                        if (velocityDir.LengthSquared() > 0.1f)
                        {
                            desiredDirection = -Vector2.Normalize(velocityDir);
                            needsCorrection = true;
                        }
                    }

                    // Always counter gravity if present
                    if (gravityForce.LengthSquared() > 0.1f)
                    {
                        Vector2 antiGravity = -Vector2.Normalize(gravityForce);

                        if (needsCorrection)
                        {
                            // Blend position correction with gravity compensation
                            desiredDirection = Vector2.Normalize(desiredDirection + antiGravity * 0.7f);
                        }
                        else
                        {
                            // Only gravity compensation needed
                            desiredDirection = antiGravity;
                            needsCorrection = true;
                        }
                    }

                    // Apply corrective thrust if needed
                    if (needsCorrection && desiredDirection.LengthSquared() > 0.01f)
                    {
                        // Calculate target rotation angle
                        float correctionAngle = (float)Math.Atan2(desiredDirection.Y, desiredDirection.X);

                        // Rotate toward correction direction
                        const float holdAlignmentThreshold = 0.2f;
                        bool isAligned = Owner.RotateTowards(correctionAngle, deltaTime, holdAlignmentThreshold);

                        // Apply thrust proportional to gravity strength and position error
                        if (isAligned)
                        {
                            float gravityStrength = gravityForce.Length() / (Owner.ShipStats.ThrustForce * GravityWell.GravityThrustRatio);
                            float thrustMultiplier = Math.Max(0.3f, Math.Clamp(gravityStrength + 0.5f, 0.3f, 1.0f));
                            Owner.ApplyThrust(deltaTime * thrustMultiplier);
                        }
                    }
                    break;

                case TaskState.Complete:
                case TaskState.Invalid:
                    // Task is complete, remain in this state
                    break;
            }
        }
    }
}
