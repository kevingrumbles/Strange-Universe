using Microsoft.Xna.Framework;
using Strange_Universe.Game.Entities;
using StrangeUniverse;
using System;
using System.Linq;

namespace Strange_Universe.Game.NavSystem
{
    /// <summary>
    /// Handles guarding a specific location by maintaining an orbital patrol around it.
    /// Uses Ship's RotateTowards and ApplyThrust methods to control navigation.
    /// </summary>
    public class GuardTask : NavTask
    {
        private Vector2 _target;
        private const float ApproachThreshold = 600f; // When to transition to orbit
        private float _orbitRadius;

        public GuardTask(Ship owner, Vector2 target, float orbitRadius = 500f) : base(owner)
        {
            _target = target;
            _orbitRadius = orbitRadius;
            CurrentState = TaskState.ApproachPosition;
        }

        public override void Update(float deltaTime)
        {
            if (Owner == null) return;

            switch (CurrentState)
            {
                case TaskState.ApproachPosition:
                    // APPROACH PHASE: Move at full speed toward orbit position
                    float distanceFromTarget = Vector2.Distance(Owner.Position, _target);

                    // Check if we're close enough to enter orbit
                    if (distanceFromTarget <= ApproachThreshold)
                    {
                        CurrentState = TaskState.Guarding;
                        break;
                    }

                    Vector2 directionToTarget = _target - Owner.Position;

                    if (directionToTarget.LengthSquared() > 0.1f)
                    {
                        directionToTarget.Normalize();

                        // Calculate target rotation angle
                        float targetAngle = (float)Math.Atan2(directionToTarget.Y, directionToTarget.X);

                        // Rotate toward target
                        const float alignmentThreshold = 0.15f; // ~8.6 degrees
                        bool isAligned = Owner.RotateTowards(targetAngle, deltaTime, alignmentThreshold);

                        // Apply full thrust when aligned
                        if (isAligned)
                        {
                            Owner.ApplyThrust(deltaTime);
                        }
                    }
                    break;

                case TaskState.Guarding:
                    // ORBIT PHASE: Maintain circular orbit close to target

                    // Calculate vector from target to owner
                    Vector2 toOwner = Owner.Position - _target;
                    float distanceFromGuardTarget = toOwner.Length();

                    // Normalize radial vector
                    if (distanceFromGuardTarget < 10f)
                    {
                        // Very close to target - pick a direction to move away
                        Random rand = new Random();
                        float randomAngle = (float)(rand.NextDouble() * Math.PI * 2);
                        toOwner = new Vector2((float)Math.Cos(randomAngle), (float)Math.Sin(randomAngle)) * _orbitRadius;
                        distanceFromGuardTarget = _orbitRadius;
                    }
                    else
                    {
                        toOwner = Vector2.Normalize(toOwner);
                    }

                    // Calculate perpendicular vector for orbital motion (90 degrees counterclockwise)
                    Vector2 tangentDirection = new Vector2(-toOwner.Y, toOwner.X);

                    // Calculate how far we are from ideal orbit radius
                    float radiusError = distanceFromGuardTarget - _orbitRadius;
                    float radiusErrorRatio = Math.Abs(radiusError) / _orbitRadius;

                    // Determine desired direction based on distance from orbit
                    Vector2 desiredDirection;
                    if (distanceFromGuardTarget > _orbitRadius * 1.05f)
                    {
                        // Too far - move inward with stronger correction
                        float correction = Math.Clamp(radiusErrorRatio * 3f, 0.8f, 3f);
                        desiredDirection = Vector2.Normalize(tangentDirection - toOwner * correction);
                    }
                    else if (distanceFromGuardTarget < _orbitRadius * 0.95f)
                    {
                        // Too close - move outward with stronger correction
                        float correction = Math.Clamp(radiusErrorRatio * 3f, 0.8f, 3f);
                        desiredDirection = Vector2.Normalize(tangentDirection + toOwner * correction);
                    }
                    else
                    {
                        // At correct distance - maintain pure tangential motion
                        desiredDirection = tangentDirection;
                    }

                    // Calculate target angle
                    float orbitTargetAngle = (float)Math.Atan2(desiredDirection.Y, desiredDirection.X);

                    // Rotate toward desired direction
                    const float orbitAlignmentThreshold = 0.2f; // ~11 degrees
                    bool isOrbitAligned = Owner.RotateTowards(orbitTargetAngle, deltaTime, orbitAlignmentThreshold);

                    // Apply thrust to maintain orbital motion
                    if (isOrbitAligned)
                    {
                        Owner.ApplyThrust(deltaTime);
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
