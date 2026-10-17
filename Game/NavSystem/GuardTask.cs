using Microsoft.Xna.Framework;
using Strange_Universe.Game.Entities.ShipParts;
using System;

namespace Strange_Universe.Game.NavSystem
{
    /// <summary>
    /// Guards a location by flying a circular orbit around it. The ship first flies to the orbit ring,
    /// slowing to orbit speed on the way (it never aims at the target's center), then keeps its velocity
    /// matched to "tangent at orbit speed plus a radial correction" so the radius holds.
    /// Flight control comes from <see cref="Steering"/>.
    /// </summary>
    public class GuardTask : NavTask
    {
        private readonly Vector2 _target;
        private readonly float _orbitRadius;

        /// <summary>Orbit speed as a fraction of the ship's max speed (upper bound; see <see cref="OrbitSpeedFor"/>).</summary>
        public const float OrbitSpeedFraction = 0.5f;

        /// <summary>
        /// Share of the ship's acceleration that may go to turning the orbit. Orbiting at speed v and radius r
        /// needs v²/r, which must stay well below what the engine can deliver.
        /// </summary>
        public const float CentripetalBudget = 0.5f;

        /// <summary>The ship starts orbiting once it is this much farther than the orbit radius from the target.</summary>
        public const float EntryMargin = 300f;

        /// <summary>Radial speed per unit of radius error (1/s), so a ship off the ring drifts back to it smoothly.</summary>
        public const float RadialGain = 1f;

        /// <summary>An orbiting ship farther than the orbit radius plus this many entry margins goes back to approaching.</summary>
        public const float ReapproachFactor = 3f;

        public GuardTask(Ship owner, Vector2 target, float orbitRadius = 500f) : base(owner)
        {
            _target = target;
            _orbitRadius = Math.Max(1f, orbitRadius);
            CurrentPhase = Phase.Approach;
        }

        /// <summary>The stages of a guard task.</summary>
        public enum Phase
        {
            /// <summary>Flying to the orbit ring.</summary>
            Approach,

            /// <summary>Circling the target. A guard never finishes on its own.</summary>
            Orbit,
        }

        public Phase CurrentPhase { get; private set; }

        /// <summary>A guard hunts down whoever attacks its ship.</summary>
        public override TaskDisposition Disposition => TaskDisposition.Aggressive;

        public Vector2 Target => _target;
        public float OrbitRadius => _orbitRadius;

        /// <summary>Speed the ship flies its orbit at: slow enough that the engine can supply the centripetal acceleration.</summary>
        public float OrbitSpeedFor(Ship ship)
        {
            float centripetalLimited = MathF.Sqrt(ship.MaxAcceleration * CentripetalBudget * _orbitRadius);
            return Math.Min(ship.ShipType.Flight.MaxSpeed * OrbitSpeedFraction, centripetalLimited);
        }

        public override void Update(float deltaTime)
        {
            if (Owner == null) return;

            switch (CurrentPhase)
            {
                case Phase.Approach:
                    Vector2 fromTarget = Owner.Position - _target;
                    float distanceFromTarget = fromTarget.Length();

                    if (distanceFromTarget <= _orbitRadius + EntryMargin)
                    {
                        CurrentPhase = Phase.Orbit;
                        break;
                    }

                    // Aim at the ring point facing the ship, so the approach never heads for the target's center.
                    Vector2 outward = fromTarget / distanceFromTarget;
                    Vector2 ringPoint = _target + outward * _orbitRadius;
                    Vector2 toRing = ringPoint - Owner.Position;

                    Steering.Arrive(Owner, toRing, toRing.Length() - EntryMargin, OrbitSpeedFor(Owner), deltaTime);
                    break;

                case Phase.Orbit:
                    // Pulled far off the ring (for example after chasing a hostile): approach it again.
                    if (Vector2.Distance(Owner.Position, _target) > _orbitRadius + EntryMargin * ReapproachFactor)
                    {
                        CurrentPhase = Phase.Approach;
                        break;
                    }

                    Vector2 radial = Owner.Position - _target;
                    float distance = radial.Length();
                    // Exactly on top of the target there is no radial direction; pick a fixed one.
                    radial = distance > 1f ? radial / distance : Vector2.UnitX;

                    // Counterclockwise tangent.
                    Vector2 tangent = new Vector2(-radial.Y, radial.X);
                    float orbitSpeed = OrbitSpeedFor(Owner);

                    // Radius error becomes a radial speed back toward the ring (positive error = too far out).
                    float radialSpeed = Math.Clamp(-(distance - _orbitRadius) * RadialGain, -orbitSpeed, orbitSpeed);
                    Vector2 desiredVelocity = tangent * orbitSpeed + radial * radialSpeed;

                    Vector2 steer = desiredVelocity - Owner.Velocity;
                    // The tolerance is relative to the orbit speed, not the ship's top speed: a faster ship must not orbit more sloppily.
                    if (steer.Length() >= orbitSpeed * Steering.VelocityTolerance)
                        Steering.Seek(Owner, steer, deltaTime);
                    break;
            }
        }
    }
}
