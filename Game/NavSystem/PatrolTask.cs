using Microsoft.Xna.Framework;
using Strange_Universe.Game.Entities;
using StrangeUniverse;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Strange_Universe.Game.NavSystem
{
    /// <summary>
    /// Handles automated patrol navigation through a series of waypoints.
    /// Uses Ship's RotateTowards and ApplyThrust methods to control navigation through each patrol point.
    /// </summary>
    public class PatrolTask : NavTask
    {
        public List<Vector2> PatrolPoints { get; private set; }
        private int _currentPatrolIndex = 0;
        private const float ArrivalThreshold = 500f; // Distance to consider patrol point "reached"
        private const float CollisionAvoidanceRadius = 2000f; // Distance to start avoiding obstacles

        public PatrolTask(Ship owner, List<Vector2> patrolPoints) : base(owner)
        {
            CurrentState = TaskState.Patrolling;
            PatrolPoints = patrolPoints;
            _currentPatrolIndex = 0;
        }

        public override void Update(float deltaTime)
        {
            if (Owner == null) return;

            switch (CurrentState)
            {
                case TaskState.Patrolling:
                    if (PatrolPoints == null || PatrolPoints.Count == 0)
                    {
                        CurrentState = TaskState.Complete;
                        return;
                    }

                    // Get current patrol target
                    Vector2 targetPatrolPoint = PatrolPoints[_currentPatrolIndex];

                    // Check if we've reached the current patrol point
                    float distanceToTarget = Vector2.Distance(Owner.Position, targetPatrolPoint);
                    if (distanceToTarget < ArrivalThreshold)
                    {
                        // Move to next patrol point
                        _currentPatrolIndex = (_currentPatrolIndex + 1) % PatrolPoints.Count;
                        targetPatrolPoint = PatrolPoints[_currentPatrolIndex];
                    }

                    // Calculate direction to target
                    Vector2 directionToTarget = targetPatrolPoint - Owner.Position;

                    if (directionToTarget.LengthSquared() > 0.1f)
                    {
                        // Check for collision threats and adjust direction if needed
                        Vector2 adjustedDirection = GetCollisionAvoidanceDirection(directionToTarget);

                        // Calculate target rotation angle
                        float targetAngle = (float)Math.Atan2(adjustedDirection.Y, adjustedDirection.X);

                        // Rotate toward target direction
                        const float alignmentThreshold = 0.15f; // ~8.6 degrees
                        bool isAligned = Owner.RotateTowards(targetAngle, deltaTime, alignmentThreshold);

                        // Apply thrust when aligned with target
                        if (isAligned)
                        {
                            Owner.ApplyThrust(deltaTime);
                        }

                        // Speed management - slow down when approaching patrol point
                        if (distanceToTarget < ArrivalThreshold * 3f && Owner.Speed > Owner.ShipStats.MaxSpeed * 0.3f)
                        {
                            // Decelerate when close to waypoint
                            Vector2 velocityDir = Owner.Velocity;
                            if (velocityDir.LengthSquared() > 0.1f)
                            {
                                float retrogradeAngle = (float)Math.Atan2(-velocityDir.Y, -velocityDir.X);
                                Owner.RotateTowards(retrogradeAngle, deltaTime, 0.3f);
                            }
                        }
                    }
                    break;
                    
                case TaskState.Alerting:
                    // Handle alerting behavior here
                    break;

                case TaskState.Complete:
                case TaskState.Invalid:
                    // Task is complete, remain in this state
                    break;
            }
        }

        /// <summary>
        /// Calculates a safe direction that avoids collisions with stars and planets.
        /// Returns the original direction if no obstacles detected, or an adjusted direction to avoid them.
        /// </summary>
        private Vector2 GetCollisionAvoidanceDirection(Vector2 desiredDirection)
        {
            Vector2 avoidanceVector = Vector2.Zero;

            // Check for star collisions
            if (Owner.StarSystem?.Stars != null)
            {
                foreach (var star in Owner.StarSystem.Stars)
                {
                    float distance = Vector2.Distance(Owner.Position, star.Position);
                    float dangerRadius = star.Radius * 2.5f;

                    if (distance < CollisionAvoidanceRadius && Owner.IsApproachingCollision(star.Position, dangerRadius, 3f))
                    {
                        // Add avoidance vector away from star
                        Vector2 awayFromStar = Owner.Position - star.Position;
                        if (awayFromStar.LengthSquared() > 0.1f)
                        {
                            awayFromStar.Normalize();
                            // Stronger avoidance the closer we are
                            float avoidanceStrength = 1f - (distance / CollisionAvoidanceRadius);
                            avoidanceVector += awayFromStar * avoidanceStrength * 2f;
                        }
                    }
                }
            }

            // Check for planet collisions
            if (Owner.StarSystem?.Planets != null)
            {
                foreach (var planet in Owner.StarSystem.Planets)
                {
                    float distance = Vector2.Distance(Owner.Position, planet.Position);
                    float dangerRadius = planet.Radius * 2f;

                    if (distance < CollisionAvoidanceRadius && Owner.IsApproachingCollision(planet.Position, dangerRadius, 3f))
                    {
                        // Add avoidance vector away from planet
                        Vector2 awayFromPlanet = Owner.Position - planet.Position;
                        if (awayFromPlanet.LengthSquared() > 0.1f)
                        {
                            awayFromPlanet.Normalize();
                            // Stronger avoidance the closer we are
                            float avoidanceStrength = 1f - (distance / CollisionAvoidanceRadius);
                            avoidanceVector += awayFromPlanet * avoidanceStrength * 2f;
                        }
                    }
                }
            }

            // If avoidance is needed, blend with desired direction
            if (avoidanceVector.LengthSquared() > 0.1f)
            {
                avoidanceVector.Normalize();
                Vector2 normalizedDesired = Vector2.Normalize(desiredDirection);

                // Blend avoidance with desired direction (favor avoidance when threatened)
                Vector2 blendedDirection = normalizedDesired + avoidanceVector * 1.5f;
                if (blendedDirection.LengthSquared() > 0.1f)
                {
                    return Vector2.Normalize(blendedDirection);
                }
            }

            // No avoidance needed, return original direction
            return Vector2.Normalize(desiredDirection);
        }
    }
}
