using Microsoft.Xna.Framework;
using Strange_Universe.Game.Entities;
using System;
using System.Collections.Generic;

namespace Strange_Universe.Game.NavSystem
{
    /// <summary>
    /// Flies a loop through a series of waypoints. The ship slows down to a corner speed as it reaches
    /// each waypoint, then accelerates toward the next one, steering around stars and planets.
    /// Flight control comes from <see cref="Steering"/>. A patrol with no waypoints completes immediately.
    /// </summary>
    public class PatrolTask : NavTask
    {
        public List<Vector2> PatrolPoints { get; private set; }
        private int _currentPatrolIndex = 0;
        private float _closestApproach = float.MaxValue;

        /// <summary>Index of the waypoint the ship is currently heading for.</summary>
        public int CurrentWaypointIndex => _currentPatrolIndex;

        /// <summary>Distance at which a waypoint counts as reached.</summary>
        public const float ArrivalThreshold = 500f;

        /// <summary>Speed through a waypoint, as a fraction of the ship's max speed.</summary>
        public const float CornerSpeedFraction = 0.4f;

        /// <summary>
        /// A waypoint also counts as reached when the ship got within this multiple of <see cref="ArrivalThreshold"/>
        /// and is now moving away: sideways momentum can carry a slow ship past the waypoint without entering the radius.
        /// </summary>
        public const float PassByFactor = 2f;

        /// <summary>How much farther than its closest approach the ship must be before it counts as moving away.</summary>
        private const float MovingAwayMargin = 50f;

        public PatrolTask(Ship owner, List<Vector2> patrolPoints) : base(owner)
        {
            PatrolPoints = patrolPoints;
            _currentPatrolIndex = 0;
        }

        public override void Update(float deltaTime)
        {
            if (Owner == null) return;

            if (PatrolPoints == null || PatrolPoints.Count == 0)
            {
                Complete();
                return;
            }

            Vector2 target = PatrolPoints[_currentPatrolIndex];
            float distance = Vector2.Distance(Owner.Position, target);
            _closestApproach = Math.Min(_closestApproach, distance);
            bool passedBy = _closestApproach < ArrivalThreshold * PassByFactor && distance > _closestApproach + MovingAwayMargin;
            if (distance < ArrivalThreshold || passedBy)
            {
                // Reached it: head for the next waypoint, measured from where the ship is now.
                _currentPatrolIndex = (_currentPatrolIndex + 1) % PatrolPoints.Count;
                _closestApproach = float.MaxValue;
                target = PatrolPoints[_currentPatrolIndex];
                distance = Vector2.Distance(Owner.Position, target);
            }

            Vector2 toTarget = target - Owner.Position;
            if (toTarget.LengthSquared() <= 0.1f)
                return;

            Vector2 direction = Steering.AvoidObstacles(Owner, World, toTarget);
            float cornerSpeed = Owner.ShipType.MaxSpeed * CornerSpeedFraction;

            // Slow for the waypoint: the ship must reach corner speed by the time it is
            // ArrivalThreshold away, not at the waypoint itself.
            Steering.Arrive(Owner, direction, distance - ArrivalThreshold, cornerSpeed, deltaTime);
        }
    }
}
