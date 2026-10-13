using Microsoft.Xna.Framework;
using Strange_Universe.Game.Entities;
using System;

namespace Strange_Universe.Game.NavSystem
{
    /// <summary>
    /// Last stage of a jump, scripted rather than flown: put the ship at the edge of the system it is arriving in,
    /// moving inward at hyperspace speed, then taper that speed down to normal travel speed halfway to the center.
    /// For the player it also switches the game to the target system. Completes at the taper point.
    /// </summary>
    public class ArrivalTask : NavTask
    {
        public enum Phase
        {
            /// <summary>Enter the target system and place the ship at its edge.</summary>
            Translate,

            /// <summary>Slow from hyperspace speed to normal speed on the way in.</summary>
            Taper,
        }

        /// <summary>Speed on arrival, as a multiple of the ship's normal maximum speed.</summary>
        public const float SpeedMultiplier = 25f;

        /// <summary>Speed has tapered to normal at this fraction of the system radius (0.5 = halfway to the center).</summary>
        public const float TaperEndFraction = 0.5f;

        private readonly string _targetSystemId;
        private readonly Vector2? _originGalaxyPosition;

        public ArrivalTask(Ship owner, Vector2? originGalaxyPosition, string targetSystemId, Phase startPhase = Phase.Translate) : base(owner)
        {
            _originGalaxyPosition = originGalaxyPosition;
            _targetSystemId = targetSystemId;
            CurrentPhase = startPhase;
        }

        public Phase CurrentPhase { get; private set; }

        public override void Update(float deltaTime)
        {
            if (Owner == null) return;

            switch (CurrentPhase)
            {
                case Phase.Translate:
                    Translate();
                    break;

                case Phase.Taper:
                    Taper();
                    break;
            }
        }

        private void Translate()
        {
            if (Owner is Player)
            {
                // Builds the new system, attaches the player and updates CurrentStarSystemID.
                World.EnterSystem(_targetSystemId);
            }

            Owner.Position = World.GetSystemEdgeEntryPosition(_originGalaxyPosition, RandomFor("arrival"));

            Vector2 inward = MathHelpers.SafeNormalize(-Owner.Position, -Vector2.UnitX);

            // Arrive at hyperspace speed, pointing inward, and face the way the ship is travelling right away;
            // otherwise it would keep its outbound heading from the jump and only turn gradually.
            Owner.Velocity = inward * (Owner.ShipType.MaxSpeed * SpeedMultiplier);
            Owner.Rotation = (float)Math.Atan2(inward.Y, inward.X);

            CurrentPhase = Phase.Taper;
        }

        private void Taper()
        {
            float radius = World?.SystemRadius ?? 50000f;
            float normalSpeed = Owner.ShipType.MaxSpeed;
            float taperEnd = radius * TaperEndFraction;
            float distance = Owner.Position.Length();

            Vector2 inward = MathHelpers.SafeNormalize(-Owner.Position, Owner.Forward);
            Owner.Rotation = (float)Math.Atan2(inward.Y, inward.X);

            if (distance <= taperEnd)
            {
                // Reached the taper point: normal speed, arrival complete.
                Owner.Velocity = inward * normalSpeed;
                Complete();
                return;
            }

            // Speed depends on position, not time, so it is frame-rate independent.
            // Quadratic ease: sheds speed quickly at first, then settles onto normal speed.
            float progress = Math.Clamp((radius - distance) / (radius - taperEnd), 0f, 1f);
            float remaining = 1f - progress;
            float speed = normalSpeed + (normalSpeed * SpeedMultiplier - normalSpeed) * remaining * remaining;
            Owner.Velocity = inward * speed;
        }
    }
}
