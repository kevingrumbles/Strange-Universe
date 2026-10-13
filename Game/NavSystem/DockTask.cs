using Microsoft.Xna.Framework;
using Strange_Universe.Game.Entities;
using System;

namespace Strange_Universe.Game.NavSystem
{
    /// <summary>
    /// Docks at a location: flies to a hold point <see cref="SafeDistance"/> from the target on the side the
    /// ship arrives from, stops there, holds station for a while, then completes.
    /// Both phases use <see cref="Steering.Arrive"/> (arrival speed zero), so the ship brakes in time instead of
    /// overshooting, and any drift while holding is corrected by the same velocity matching.
    /// </summary>
    public class DockTask : NavTask
    {
        private readonly Vector2 _target;
        private Vector2? _holdPoint;
        private float _holdTimer = 0f;
        private readonly float _holdDuration;

        /// <summary>Distance from the target at which the ship holds station.</summary>
        public const float SafeDistance = 500f;

        /// <summary>The approach ends when the ship is this close to the hold point and nearly stopped.</summary>
        public const float HoldArrivalDistance = 100f;

        /// <summary>Speed (as a fraction of max speed) below which the ship counts as stopped on arrival.</summary>
        public const float HoldArrivalSpeedFraction = 0.1f;

        /// <summary>Tighter than the flight default: a ship holding station should not coast away at speeds the default ignores.</summary>
        public const float HoldVelocityTolerance = 0.01f;

        public const float MinHoldSeconds = 15f;
        public const float MaxHoldSeconds = 60f;

        public DockTask(Ship owner, Vector2 target, float? holdDuration = null) : base(owner)
        {
            _target = target;
            CurrentState = TaskState.ApproachPosition;

            // Default hold time is between 15 and 60 seconds, derived from the ship so it is reproducible.
            var rand = new Random(ProceduralHelpers.SeedHash($"{owner?.Id ?? owner?.Name}_dock"));
            _holdDuration = holdDuration ?? MinHoldSeconds + (float)(rand.NextDouble() * (MaxHoldSeconds - MinHoldSeconds));
        }

        public Vector2 Target => _target;

        /// <summary>Seconds the ship holds station before the task completes.</summary>
        public float HoldDuration => _holdDuration;

        /// <summary>The point the ship holds at. Null until the first update fixes it from the ship's position.</summary>
        public Vector2? HoldPoint => _holdPoint;

        public override void Update(float deltaTime)
        {
            if (Owner == null) return;

            switch (CurrentState)
            {
                case TaskState.ApproachPosition:
                    Vector2 hold = EnsureHoldPoint();
                    Vector2 toHold = hold - Owner.Position;
                    float distance = toHold.Length();

                    if (distance <= HoldArrivalDistance &&
                        Owner.Speed <= Owner.ShipType.MaxSpeed * HoldArrivalSpeedFraction)
                    {
                        CurrentState = TaskState.HoldPosition;
                        _holdTimer = 0f;
                        break;
                    }

                    Steering.Arrive(Owner, toHold, distance, arrivalSpeed: 0f, deltaTime);
                    break;

                case TaskState.HoldPosition:
                    _holdTimer += deltaTime;
                    if (_holdTimer >= _holdDuration)
                    {
                        CurrentState = TaskState.Complete;
                        break;
                    }

                    // Station-keeping: the same arrival logic, aimed at the hold point.
                    Vector2 toHoldPoint = _holdPoint.Value - Owner.Position;
                    Steering.Arrive(Owner, toHoldPoint, toHoldPoint.Length(), arrivalSpeed: 0f, deltaTime, HoldVelocityTolerance);
                    break;

                case TaskState.Complete:
                case TaskState.Invalid:
                    // Task is complete, remain in this state
                    break;
            }
        }

        /// <summary>Fixes the hold point on the side of the target the ship is on, so the approach never heads for the target's center.</summary>
        private Vector2 EnsureHoldPoint()
        {
            if (_holdPoint is Vector2 existing)
                return existing;

            Vector2 fromTarget = Owner.Position - _target;
            // Exactly on top of the target there is no side to pick; use a fixed one.
            Vector2 side = fromTarget.LengthSquared() > 1f ? Vector2.Normalize(fromTarget) : Vector2.UnitX;
            _holdPoint = _target + side * SafeDistance;
            return _holdPoint.Value;
        }
    }
}
