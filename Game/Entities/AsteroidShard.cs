using Microsoft.Xna.Framework;

namespace Strange_Universe.Game.Entities;

/// <summary>
/// A fragment thrown off an asteroid when a projectile strikes it. Shards drift
/// outward, tumble, and fade out over their lifetime.
/// </summary>
public class AsteroidShard
{
    public Vector2 Position        { get; set; }
    public Vector2 Velocity        { get; set; }
    public float   Rotation        { get; set; }
    public float   AngularVelocity { get; set; }
    public float   Radius          { get; set; }

    /// <summary>Texture of the parent asteroid, so shards match the rock they came from.</summary>
    public string TextureId { get; init; }

    /// <summary>Remaining lifetime in seconds.</summary>
    public float Life { get; set; }

    public float MaxLife { get; init; }

    public bool IsExpired => Life <= 0f;

    /// <summary>Fade factor from 1 (just spawned) to 0 (gone).</summary>
    public float Alpha => MaxLife <= 0f ? 0f : MathHelper.Clamp(Life / MaxLife, 0f, 1f);

    public void Update(float deltaTime)
    {
        Life     -= deltaTime;
        Position += Velocity * deltaTime;
        Rotation += AngularVelocity * deltaTime;

        // Gentle drag so shards settle rather than flying forever.
        Velocity *= 1f - MathHelper.Clamp(1.2f * deltaTime, 0f, 1f);
    }
}
