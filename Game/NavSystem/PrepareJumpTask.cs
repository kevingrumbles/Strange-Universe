using Microsoft.Xna.Framework;
using Strange_Universe.Game.Entities.ShipParts;

namespace Strange_Universe.Game.NavSystem
{
    /// <summary>
    /// First stage of a jump, flown by the autopilot (<see cref="Steering"/>): get out beyond the Mandeville
    /// radius, slow down, and turn to face away from the system center. Completes when the ship is ready to jump.
    /// </summary>
    public class PrepareJumpTask : NavTask
    {
        public enum Phase
        {
            /// <summary>Fly straight out from the system center until the ship is beyond the Mandeville radius and clear of gravity.</summary>
            MoveToMandeville,

            /// <summary>Slow to a safe speed for the jump.</summary>
            Decelerate,

            /// <summary>Turn to face away from the system center.</summary>
            AlignForJump,
        }

        /// <summary>The ship may jump at or below this speed (units per second).</summary>
        public const float SafeJumpSpeed = 50f;

        /// <summary>Alignment tolerance before the jump, tighter than normal flight (about 3 degrees).</summary>
        public const float AlignmentTolerance = 0.05f;

        public PrepareJumpTask(Ship owner, Phase startPhase = Phase.MoveToMandeville) : base(owner)
        {
            CurrentPhase = startPhase;
        }

        public Phase CurrentPhase { get; private set; }

        public override void Update(float deltaTime)
        {
            if (Owner == null) return;

            switch (CurrentPhase)
            {
                case Phase.MoveToMandeville:
                    if (Owner.CanJump())
                    {
                        CurrentPhase = Phase.Decelerate;
                        break;
                    }

                    // Fly straight out from the system center (face right when exactly at the center).
                    // Matching an outbound velocity also cancels any sideways drift the ship started with.
                    Vector2 outbound = MathHelpers.SafeNormalize(Owner.Position, Vector2.UnitX);
                    Vector2 steer = outbound * Owner.ShipType.Flight.MaxSpeed - Owner.Velocity;
                    Steering.Seek(Owner, steer, deltaTime);
                    break;

                case Phase.Decelerate:
                    if (Owner.Speed <= SafeJumpSpeed)
                        CurrentPhase = Phase.AlignForJump;
                    else
                        Steering.Brake(Owner, deltaTime);
                    break;

                case Phase.AlignForJump:
                    // Rotate only, to point away from the system center.
                    Vector2 jumpDirection = MathHelpers.SafeNormalize(Owner.Position, Owner.Forward);
                    if (Steering.Seek(Owner, jumpDirection, deltaTime, AlignmentTolerance, thrust: false))
                        Complete();
                    break;
            }
        }
    }
}
