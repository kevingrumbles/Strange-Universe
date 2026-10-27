using Strange_Universe.Game.Entities;
using System;
using Microsoft.Xna.Framework;
using Strange_Universe.Game.Components;
using Strange_Universe.Game.Entities.ShipParts;

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
            return Math.Min(allowed, ship.ShipType.Flight.MaxSpeed);
        }

        /// <summary>
        /// Flies toward <paramref name="direction"/> at the highest speed that still lets the ship reach
        /// <paramref name="arrivalSpeed"/> at <paramref name="distance"/>, correcting both speed and sideways drift.
        /// </summary>
        public static void Arrive(Ship ship, Vector2 direction, float distance, float arrivalSpeed, float deltaTime,
            float? velocityTolerance = null)
        {
            if (direction.LengthSquared() < 1e-6f)
                return;

            // Braking cannot start until the ship has turned around, and it keeps moving meanwhile.
            float turnAroundTime = ship.ShipType.Flight.RotationSpeed > 0f ? MathF.PI / ship.ShipType.Flight.RotationSpeed : 0f;
            float allowed = AllowedApproachSpeed(ship, distance - ship.Speed * turnAroundTime, arrivalSpeed);

            // Velocity matching: steer so the velocity converges on (direction x allowed speed). This brakes
            // when too fast and also cancels sideways drift, which speed-only control would leave as an orbit.
            Vector2 desiredVelocity = Vector2.Normalize(direction) * allowed;
            Vector2 steer = desiredVelocity - ship.Velocity;
            if (steer.Length() < (velocityTolerance ?? ship.ShipType.Flight.MaxSpeed * VelocityTolerance))
                return; // close enough: coast

            Seek(ship, steer, deltaTime);
        }

        /// <summary>
        /// Bends <paramref name="desiredDirection"/> away from stars and planets the ship is about to hit.
        /// Returns the normalized desired direction when nothing is in the way.
        /// </summary>
        public static Vector2 AvoidObstacles(Ship ship, StarSystem world, Vector2 desiredDirection, float avoidanceRadius = 2000f)
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

        /// <summary>
        /// Steers so the velocity converges on <paramref name="desiredVelocity"/>. Returns true when it already matches
        /// closely enough (within <paramref name="velocityTolerance"/> units per second; a fraction of top speed by default) that the ship is just coasting.
        /// </summary>
        public static bool MatchVelocity(Ship ship, Vector2 desiredVelocity, float deltaTime, float? velocityTolerance = null)
        {
            Vector2 steer = desiredVelocity - ship.Velocity;
            if (steer.Length() < (velocityTolerance ?? ship.ShipType.Flight.MaxSpeed * VelocityTolerance))
                return true;

            Seek(ship, steer, deltaTime);
            return false;
        }

        /// <summary>
        /// Flies toward a moving target and settles at <paramref name="stopDistance"/> from it, moving along with it.
        /// The approach speed is relative to the target, so the ship slows in time instead of overshooting.
        /// </summary>
        public static void Pursue(Ship ship, Vector2 targetPosition, Vector2 targetVelocity, float stopDistance, float deltaTime)
            => MatchVelocity(ship, PursuitVelocity(ship, targetPosition, targetVelocity, stopDistance), deltaTime, CorrectableSpeed(ship, PursuitToleranceSeconds));

        /// <summary>The velocity a ship should have to close in on a moving target and settle <paramref name="stopDistance"/> from it.</summary>
        public static Vector2 PursuitVelocity(Ship ship, Vector2 targetPosition, Vector2 targetVelocity, float stopDistance)
        {
            Vector2 toTarget = targetPosition - ship.Position;
            float distance = toTarget.Length();
            if (distance < 1f)
            {
                return targetVelocity;
            }

            // Closing speed from which the ship can still stop relative to the target at stopDistance,
            // allowing for the time it takes to turn around.
            float turnAroundTime = ship.ShipType.Flight.RotationSpeed > 0f ? MathF.PI / ship.ShipType.Flight.RotationSpeed : 0f;
            // The turn-around allowance depends on how fast the ship is moving relative to the target, not on its own speed:
            // a ship cruising alongside a moving target is not closing on it at all.
            float relativeSpeed = (ship.Velocity - targetVelocity).Length();
            float planned = AllowedApproachSpeed(ship, distance - stopDistance - relativeSpeed * turnAroundTime, 0f);

            // Near the stop distance the closing speed follows the distance error (backing off when too close), so the
            // ship settles there instead of coasting through at a speed too small to correct.
            float backOff = ship.ShipType.Flight.MaxSpeed * PursuitBackOffFraction;
            float closing = Math.Min(planned, Math.Max(-backOff, (distance - stopDistance) * PursuitGain));

            Vector2 desired = targetVelocity + toTarget / distance * closing;
            if (desired.Length() > ship.ShipType.Flight.MaxSpeed)
                desired = Vector2.Normalize(desired) * ship.ShipType.Flight.MaxSpeed;

            return desired;
        }

        /// <summary>
        /// The direction to point so that a shot from <paramref name="ship"/> meets a target moving at a constant velocity.
        /// Shots inherit the shooter's velocity, so the target's motion is taken relative to it. Zero when the ship is unarmed.
        /// </summary>
        public static Vector2 LeadDirection(Ship ship, Vector2 targetPosition, Vector2 targetVelocity)
        {
            float shotSpeed = ship.ProjectileSpeed;
            Vector2 relativePosition = targetPosition - ship.Position;
            if (shotSpeed <= 0f || relativePosition.LengthSquared() < 1e-6f)
                return Vector2.Zero;

            Vector2 relativeVelocity = targetVelocity - ship.Velocity;
            Vector2 aim = relativePosition;
            for (int i = 0; i < 2; i++) // two refinements are plenty at these speeds
            {
                float flightTime = aim.Length() / shotSpeed;
                aim = relativePosition + relativeVelocity * flightTime;
            }
            return aim.LengthSquared() < 1e-6f ? Vector2.Zero : Vector2.Normalize(aim);
        }

        /// <summary>Turns toward the lead direction for a target. Returns whether the ship is now aimed within <paramref name="alignment"/>.</summary>
        public static bool AimAt(Ship ship, Vector2 targetPosition, Vector2 targetVelocity, float deltaTime, float alignment = AimTolerance)
        {
            Vector2 lead = LeadDirection(ship, targetPosition, targetVelocity);
            return lead != Vector2.Zero && Seek(ship, lead, deltaTime, alignment, thrust: false);
        }

        /// <summary>Fires if the ship is already pointing at the lead direction and the target is within range. Returns whether it was aimed and in range.</summary>
        public static bool FireIfAimed(Ship ship, Vector2 targetPosition, Vector2 targetVelocity)
        {
            float range = ship.WeaponRange;
            if (range <= 0f || Vector2.Distance(ship.Position, targetPosition) > range * FireRangeFraction)
                return false;

            Vector2 lead = LeadDirection(ship, targetPosition, targetVelocity);
            if (lead == Vector2.Zero || Vector2.Dot(Vector2.Normalize(ship.Forward), lead) < MathF.Cos(AimTolerance))
                return false;

            ship.FireWeapons();
            return true;
        }

        /// <summary>
        /// Pursuit corrects velocity errors larger than what the engine can remove in this many seconds. Tighter than normal
        /// flight (a pursuer should end up level with its target, not coasting just short of it), and tied to the ship's
        /// acceleration rather than its top speed, so a faster ship is not any sloppier.
        /// </summary>
        public const float PursuitToleranceSeconds = 0.032f;

        /// <summary>The speed error the main engine can remove in <paramref name="seconds"/>. A precision limit that does not depend on top speed.</summary>
        public static float CorrectableSpeed(Ship ship, float seconds) => ship.MaxAcceleration * seconds;

        /// <summary>Closing speed per unit of distance error (1/s) when a pursuer is near its stop distance.</summary>
        public const float PursuitGain = 1.5f;

        /// <summary>The fastest a pursuer backs off when it is closer than its stop distance, as a fraction of max speed.</summary>
        public const float PursuitBackOffFraction = 0.25f;

        /// <summary>How closely a ship must be pointing at the lead direction to fire (radians, about 4.6 degrees).</summary>
        public const float AimTolerance = 0.08f;

        /// <summary>A ship fires only at targets within this fraction of its weapon range, so shots do not expire on the way.</summary>
        public const float FireRangeFraction = 0.9f;
    }
}
