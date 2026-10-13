using Microsoft.Xna.Framework;
using Strange_Universe.Game.Entities;

namespace Strange_Universe.Game.NavSystem
{
    /// <summary>
    /// Places the ship at a safe entry position in its system, stationary. Finishes on its first update.
    /// </summary>
    public class SpawnTask : NavTask
    {
        public SpawnTask(Ship owner) : base(owner)
        {
        }

        public override void Update(float deltaTime)
        {
            if (Owner == null) return;

            Owner.Position = World.GetSafeEntryTransform().Position;
            Owner.Velocity = Vector2.Zero; // Reset velocity after spawning
            Complete();
        }
    }
}
