using System;
using Strange_Universe.Game.Entities;

namespace Strange_Universe.Game.NavSystem
{
    /// <summary>
    /// Second stage of a jump, scripted rather than flown: the jump drive accelerates the ship along its facing,
    /// ignoring the normal speed limit, until it is beyond the system edge at hyperspace speed. Completes there.
    /// </summary>
    public class JumpDriveTask : NavTask
    {
        /// <summary>Growth factor of the drive's acceleration over the length of the jump.</summary>
        public const float AccelerationGrowthRate = 1.5f;

        public JumpDriveTask(Ship owner) : base(owner)
        {
        }

        public override void Update(float deltaTime)
        {
            if (Owner == null) return;

            float distanceFromCenter = Owner.Position.Length();
            float systemRadius = World?.SystemRadius ?? 50000f;

            if (distanceFromCenter > systemRadius && Owner.Velocity.LengthSquared() > Owner.ShipType.Flight.MaxSpeed * 8)
            {
                Complete();
                return;
            }

            // Exponential acceleration: starts at the engine's thrust and grows the farther along the jump the ship is.
            float mandevilleRadius = World?.MandevilleRadius ?? 5000f;
            float progress = Math.Max(0f, (distanceFromCenter - mandevilleRadius) / (systemRadius - mandevilleRadius));
            float acceleration = Owner.ShipType.Flight.ThrustForce * (float)Math.Pow(AccelerationGrowthRate, progress * 10f);

            // Accelerate along the facing the ship aligned to. (Following the current velocity instead would send a
            // ship with leftover drift off sideways or even backwards.) Velocity is changed directly to bypass the
            // MaxSpeed soft cap: the jump drive overrides normal thrust limits.
            Owner.Velocity += Owner.Forward * acceleration * deltaTime;
        }
    }
}
