using Microsoft.Xna.Framework;
using Strange_Universe.Game.Entities.ShipParts;
using System;

namespace Strange_Universe.Game.NavSystem
{
    /// <summary>
    /// A complete jump from the current system to another. It runs three stages one after the other:
    /// <see cref="PrepareJumpTask"/> (flown by the autopilot), <see cref="JumpDriveTask"/> and
    /// <see cref="ArrivalTask"/> (both scripted). An NPC leaves the system after the drive stage and is removed;
    /// a player goes on to arrive in the target system.
    /// The jump as a whole is what the navigator, the player and the events queue and cancel; it also owns what
    /// happens when the jump ends (using the route entry and fuel).
    /// </summary>
    public class JumpTask : NavTask
    {
        private readonly string _targetSystemId;
        private readonly Vector2? _originGalaxyPosition;
        private NavTask _stage;
        private bool _validated;

        /// <summary>Where the jump is, across all stages.</summary>
        public enum Phase
        {
            /// <summary>Fly out from the system center until the ship is beyond the Mandeville radius and clear of gravity.</summary>
            MoveToMandeville,

            /// <summary>Slow to a safe speed for the jump.</summary>
            Decelerate,

            /// <summary>Turn to face away from the system center.</summary>
            AlignForJump,

            /// <summary>Accelerate past the normal speed limit until beyond the system edge.</summary>
            Jump,

            /// <summary>Switch to the target system and place the ship at its edge.</summary>
            SystemTranslation,

            /// <summary>Taper from hyperspace speed to normal speed on the way in.</summary>
            ArriveInSystem,
        }

        /// <summary>Speed on arrival, as a multiple of the ship's normal maximum speed.</summary>
        public const float ArrivalSpeedMultiplier = ArrivalTask.SpeedMultiplier;

        /// <summary>Arrival speed has tapered to normal at this fraction of the system radius (0.5 = halfway to the center).</summary>
        public const float ArrivalTaperEndFraction = ArrivalTask.TaperEndFraction;

        public JumpTask(Ship owner, Vector2? originGalaxyPosition = null, string targetSystemId = null, Phase? startPhase = null) : base(owner)
        {
            _targetSystemId = targetSystemId;
            _originGalaxyPosition = originGalaxyPosition;
            _stage = CreateStage(startPhase ?? Phase.MoveToMandeville);
        }

        /// <summary>Only the autopilot stage can be suspended; the drive and the arrival move the ship by script.</summary>
        public override bool CanBeInterrupted => _stage is PrepareJumpTask;

        public string TargetSystemId => _targetSystemId;

        /// <summary>The phase the jump is in, taken from its current stage. After the jump ends it stays at the last one.</summary>
        public Phase CurrentPhase => _stage switch
        {
            PrepareJumpTask prepare => prepare.CurrentPhase switch
            {
                PrepareJumpTask.Phase.MoveToMandeville => Phase.MoveToMandeville,
                PrepareJumpTask.Phase.Decelerate => Phase.Decelerate,
                _ => Phase.AlignForJump,
            },
            JumpDriveTask => Phase.Jump,
            ArrivalTask arrival => arrival.CurrentPhase == ArrivalTask.Phase.Translate ? Phase.SystemTranslation : Phase.ArriveInSystem,
            _ => Phase.MoveToMandeville,
        };

        private NavTask CreateStage(Phase phase) => phase switch
        {
            Phase.MoveToMandeville => new PrepareJumpTask(Owner, PrepareJumpTask.Phase.MoveToMandeville),
            Phase.Decelerate => new PrepareJumpTask(Owner, PrepareJumpTask.Phase.Decelerate),
            Phase.AlignForJump => new PrepareJumpTask(Owner, PrepareJumpTask.Phase.AlignForJump),
            Phase.Jump => new JumpDriveTask(Owner),
            Phase.SystemTranslation => new ArrivalTask(Owner, _originGalaxyPosition, _targetSystemId, ArrivalTask.Phase.Translate),
            _ => new ArrivalTask(Owner, _originGalaxyPosition, _targetSystemId, ArrivalTask.Phase.Taper),
        };

        /// <summary>When the owner has arrived in the target system, consumes the route entry and one unit of fuel.</summary>
        public override void OnCompleted()
        {
            var system = World;
            if (system == null || system.SystemId != _targetSystemId) return;

            system.RemoveFromJumpRoute(_targetSystemId);
            Owner.CurrentFuelLevel--;
        }

        /// <summary>
        /// A player's jump needs a target that exists in the galaxy. This is checked on the first update, against the
        /// world the task actually runs in; an unknown or missing target fails the task. NPCs jump out without a target.
        /// </summary>
        private bool TargetIsValid() =>
            Owner is not Player || (_targetSystemId != null && World?.SystemExists(_targetSystemId) == true);

        public override void Update(float deltaTime)
        {
            if (Owner == null) return;

            if (!_validated)
            {
                _validated = true;
                if (!TargetIsValid())
                {
                    Fail();
                    return;
                }
            }

            // Stages use the same world as the jump (the owner's own system unless one was assigned).
            _stage.World = AssignedWorld;
            _stage.Update(deltaTime);

            if (_stage.Status == NavTaskStatus.Invalid)
                Fail();
            else if (_stage.Status == NavTaskStatus.Complete)
                StageFinished();
        }

        /// <summary>Moves on to the next stage. The next stage starts running on the following update.</summary>
        private void StageFinished()
        {
            switch (_stage)
            {
                case PrepareJumpTask:
                    _stage = CreateStage(Phase.Jump);
                    break;

                case JumpDriveTask when Owner is Nonplayer npc:
                    // An NPC just leaves: it is removed from the system and the jump is over.
                    npc.Remove = true;
                    Complete();
                    break;

                case JumpDriveTask:
                    _stage = CreateStage(Phase.SystemTranslation);
                    break;

                default:
                    Complete();
                    break;
            }
        }
    }
}
