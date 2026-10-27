using Microsoft.Xna.Framework;
using Strange_Universe.Game.Entities.ShipParts;

namespace Strange_Universe.Game.NavSystem
{
    /// <summary>
    /// Places the ship at a safe entry position in its system, stationary. Finishes on its first update.
    /// </summary>
    public class SpawnTask : NavTask
    {
        /// <summary>Placing the ship happens in a single update; an alert must not skip it.</summary>
        public override bool CanBeInterrupted => false;

        public SpawnTask(Ship owner) : base(owner)
        {
        }

        public override void Update(float deltaTime)
        {
            if (Owner == null) return;

            Owner.Position = World.GetSafeEntryPosition(RandomFor("spawn"));
            Owner.Velocity = Vector2.Zero; // Reset velocity after spawning
            Complete();
        }
    }
}
