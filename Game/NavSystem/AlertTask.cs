using Microsoft.Xna.Framework;
using Strange_Universe.Game.Entities;
using System.Linq;

namespace Strange_Universe.Game.NavSystem
{
    /// <summary>
    /// What a ship does after it has been shot at. The navigator swaps this in for the task the ship was carrying out and
    /// keeps that task (<see cref="Previous"/>); when the alert ends, the previous task carries on where it left off.
    /// The previous task decides the behavior (<see cref="NavTask.Disposition"/>):
    /// <list type="bullet">
    /// <item><b>Aggressive</b> (patrols, guards): hunt the nearest hostile ship and attack it.</item>
    /// <item><b>Defensive</b> (docking, jumping, or no task): shoot back only at a hostile within weapon range, otherwise
    /// try to leave the system by jumping out.</item>
    /// </list>
    /// A ship with no weapons cannot attack, so it flees whatever its disposition. The alert ends when no hostile ship is
    /// left in the system.
    /// </summary>
    public class AlertTask : NavTask
    {
        public enum Phase
        {
            /// <summary>Closing in on a hostile that is out of firing position.</summary>
            Pursue,

            /// <summary>Matching the hostile's speed, aiming and firing.</summary>
            Engage,

            /// <summary>Jumping out of the system.</summary>
            Flee,
        }

        /// <summary>An aggressive ship closes in to this fraction of its weapon range, then stops to aim.</summary>
        public const float EngageRangeFraction = 0.7f;

        /// <summary>
        /// An aggressive ship switches from closing in to aiming once within this fraction of its range. Wider than the
        /// closing-in distance, because a ship chasing a moving target does not stop exactly where it aims to.
        /// </summary>
        public const float EngageEnterFraction = 0.9f;

        /// <summary>Once aiming, it keeps at it until the hostile is farther than this fraction of its range (so it does not flip back and forth).</summary>
        public const float EngageLeaveFraction = 1.0f;

        /// <summary>Speed difference to the hostile tolerated while aiming: what the engine can remove in this many seconds.</summary>
        public const float EngageToleranceSeconds = 0.08f;

        private JumpTask _flee;

        public AlertTask(Ship owner, NavTask previous) : base(owner)
        {
            Previous = previous;
        }

        /// <summary>The task this alert interrupted, to be resumed afterwards. Null if the ship had none.</summary>
        public NavTask Previous { get; }

        /// <summary>How the ship responds: taken from the task it was carrying out, defensive when there was none.</summary>
        public TaskDisposition Response => Previous?.Disposition ?? TaskDisposition.Defensive;

        public Phase CurrentPhase { get; private set; } = Phase.Pursue;

        /// <summary>The hostile ship being dealt with this frame. Null once none is left.</summary>
        public Ship Target { get; private set; }

        /// <summary>An alert is already the ship's reaction; it is never suspended for another one.</summary>
        public override bool CanBeInterrupted => false;

        public override void Update(float deltaTime)
        {
            if (Owner == null) return;

            // Once the jump away has moved past its autopilot stage it can not be paused, so the ship just finishes leaving.
            if (_flee != null && !_flee.CanBeInterrupted)
            {
                Flee(deltaTime);
                return;
            }

            Target = NearestHostile();
            if (Target == null)
            {
                Complete();
                return;
            }

            float range = Owner.WeaponRange;
            float distance = Vector2.Distance(Owner.Position, Target.Position);

            if (range <= 0f)
            {
                CurrentPhase = Phase.Flee; // unarmed: nothing to fight with
            }
            else if (Response == TaskDisposition.Aggressive)
            {
                float threshold = range * (CurrentPhase == Phase.Engage ? EngageLeaveFraction : EngageEnterFraction);
                CurrentPhase = distance > threshold ? Phase.Pursue : Phase.Engage;
            }
            else
            {
                CurrentPhase = distance <= range ? Phase.Engage : Phase.Flee;
            }

            switch (CurrentPhase)
            {
                case Phase.Pursue:
                    Steering.Pursue(Owner, Target.Position, Target.Velocity, range * EngageRangeFraction, deltaTime);
                    Steering.FireIfAimed(Owner, Target.Position, Target.Velocity); // shoot on the way if already lined up
                    break;

                case Phase.Engage:
                    Engage(deltaTime);
                    break;

                case Phase.Flee:
                    Flee(deltaTime);
                    break;
            }
        }

        /// <summary>Gets level with the hostile (holding its firing distance if aggressive), then aims at where it will be and fires.</summary>
        private void Engage(float deltaTime)
        {
            // An aggressive ship also holds its firing distance; a defensive one just stays level with the hostile.
            Vector2 desired = Response == TaskDisposition.Aggressive
                ? Steering.PursuitVelocity(Owner, Target.Position, Target.Velocity, Owner.WeaponRange * EngageRangeFraction)
                : Target.Velocity;
            bool matched = Steering.MatchVelocity(Owner, desired, deltaTime, Steering.CorrectableSpeed(Owner, EngageToleranceSeconds));
            if (!matched)
                return; // turned and thrusting to match speed; aim once that is done

            if (Steering.AimAt(Owner, Target.Position, Target.Velocity, deltaTime))
                Steering.FireIfAimed(Owner, Target.Position, Target.Velocity);
        }

        /// <summary>Leaves the system with a jump. If the ship was already jumping, that jump carries on.</summary>
        private void Flee(float deltaTime)
        {
            _flee ??= Previous is JumpTask { IsFinished: false } jump ? jump : new JumpTask(Owner);
            _flee.World = AssignedWorld;
            _flee.Update(deltaTime);

            if (_flee.IsFinished)
                Complete(); // the ship has jumped away
        }

        /// <summary>The closest ship this one considers hostile that is still in the world. Destroyed ones are forgotten.</summary>
        private Ship NearestHostile()
        {
            if (World == null) return null;

            Ship nearest = null;
            float nearestDistance = float.MaxValue;
            foreach (var ship in World.Ships.ToList())
            {
                if (!Owner.IsHostileTo(ship)) continue;

                if (ship.IsDestroyed)
                {
                    Owner.ForgetHostile(ship);
                    continue;
                }
                if (ship is Nonplayer { Remove: true }) continue; // left the system

                float distance = Vector2.DistanceSquared(Owner.Position, ship.Position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = ship;
                }
            }
            return nearest;
        }
    }
}
