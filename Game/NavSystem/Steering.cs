using System;
using Microsoft.Xna.Framework;
using Strange_Universe.Game.Components;
using Strange_Universe.Game.Entities;

namespace Strange_Universe.Game.NavSystem
{
    /// <summary>
    /// Shared autopilot primitives for nav tasks: turn toward a direction, thrust, brake, and arrive at a
    /// point at a chosen speed. Tasks decide where to go; this decides how to fly there, so the
    /// "rotate then thrust when aligned" logic lives in one place.
    /// All methods act on the ship for one frame (<c>deltaTime</c>) and use only ship rotation and thrust.
    /// </summary>
    public static class Steering
    {
        /// <summary>Default alignment tolerance (radians, about 8.6 degrees) before thrust is applied.</summary>
        public const float DefaultAlignment = 0.15f;

        /// <summary>
        /// Fraction of the ship's real acceleration used when planning a stop. Below 1 leaves room for
        /// the time it takes to turn around before braking can start.
        /// </summary>
        public const float BrakePlanningFactor = 0.5f;

        /// <summary>Velocity error, as a fraction of max speed, below which <see cref="Arrive"/> just coasts.</summary>
        public const float VelocityTolerance = 0.05f;

        /// <summary>Below this speed a ship is considered stopped and does not bother turning retrograde.</summary>
        public const float StoppedSpeed = 5f;

        /// <summary>Turns toward <paramref name="direction"/> and thrusts when aligned. Returns whether the ship was aligned.</summary>
        public static bool Seek(Ship ship, Vector2 direction, float deltaTime, float alignment = DefaultAlignment, bool thrust = true)
        {
            if (direction.LengthSquared() < 1e-6f)
                return false;

            float angle = (float)Math.Atan2(direction.Y, direction.X);
            bool aligned = ship.RotateTowards(angle, deltaTime, alignment);
            if (aligned && thrust)
                ship.ApplyThrust(deltaTime);
            return aligned;
        }

        /// <summary>Turns retrograde (against the velocity) and thrusts when aligned, slowing the ship down.</summary>
        public static void Brake(Ship ship, float deltaTime, float alignment = 0.2f)
        {
            if (ship.Speed < StoppedSpeed)
                return;

            Seek(ship, -ship.Velocity, deltaTime, alignment);
        }

        /// <summary>
        /// The highest speed from which the ship can still slow to <paramref name="arrivalSpeed"/> within
        /// <paramref name="distance"/>, capped at the ship's max speed. Uses <c>v² = v_end² + 2·a·d</c>
        /// with a deliberately conservative braking acceleration.
        /// </summary>
        public static float AllowedApproachSpeed(Ship ship, float distance, float arrivalSpeed)
        {
            float brake = ship.MaxAcceleration * BrakePlanningFactor;
            float remaining = Math.Max(0f, distance);
            float allowed = MathF.Sqrt(arrivalSpeed * arrivalSpeed + 2f * brake * remaining);
            return Math.Min(allowed, ship.ShipType.MaxSpeed);
        }

        /// <summary>
        /// Flies toward <paramref name="direction"/> at the highest speed that still lets the ship reach
        /// <paramref name="arrivalSpeed"/> at <paramref name="distance"/>, correcting both speed and sideways drift.
        /// </summary>
        public static void Arrive(Ship ship, Vector2 direction, float distance, float arrivalSpeed, float deltaTime,
            float velocityTolerance = VelocityTolerance)
        {
            if (direction.LengthSquared() < 1e-6f)
                return;

            // Braking cannot start until the ship has turned around, and it keeps moving meanwhile.
            float turnAroundTime = ship.ShipType.RotationSpeed > 0f ? MathF.PI / ship.ShipType.RotationSpeed : 0f;
            float allowed = AllowedApproachSpeed(ship, distance - ship.Speed * turnAroundTime, arrivalSpeed);

            // Velocity matching: steer so the velocity converges on (direction x allowed speed). This brakes
            // when too fast and also cancels sideways drift, which speed-only control would leave as an orbit.
            Vector2 desiredVelocity = Vector2.Normalize(direction) * allowed;
            Vector2 steer = desiredVelocity - ship.Velocity;
            if (steer.Length() < ship.ShipType.MaxSpeed * velocityTolerance)
                return; // close enough: coast

            Seek(ship, steer, deltaTime);
        }

        /// <summary>
        /// Bends <paramref name="desiredDirection"/> away from stars and planets the ship is about to hit.
        /// Returns the normalized desired direction when nothing is in the way.
        /// </summary>
        public static Vector2 AvoidObstacles(Ship ship, IWorldContext world, Vector2 desiredDirection, float avoidanceRadius = 2000f)
        {
            Vector2 desired = Vector2.Normalize(desiredDirection);
            if (world == null)
                return desired;

            Vector2 avoidance = Vector2.Zero;

            void Consider(Vector2 position, float radius, float dangerMultiplier)
            {
                float distance = Vector2.Distance(ship.Position, position);
                if (distance >= avoidanceRadius ||
                    !ship.IsApproachingCollision(position, radius * dangerMultiplier, 3f))
                    return;

                Vector2 away = ship.Position - position;
                if (away.LengthSquared() <= 0.1f)
                    return;

                away.Normalize();
                // Stronger the closer the ship is.
                avoidance += away * (1f - distance / avoidanceRadius) * 2f;
            }

            if (world.Stars != null)
                foreach (var star in world.Stars) Consider(star.Position, star.Radius, 2.5f);
            if (world.Planets != null)
                foreach (var planet in world.Planets) Consider(planet.Position, planet.Radius, 2f);

            if (avoidance.LengthSquared() <= 0.1f)
                return desired;

            avoidance.Normalize();
            Vector2 blended = desired + avoidance * 1.5f; // favor avoidance when threatened
            return blended.LengthSquared() > 0.1f ? Vector2.Normalize(blended) : desired;
        }
    }
}
