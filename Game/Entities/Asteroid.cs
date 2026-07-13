using Microsoft.Xna.Framework;
using StrangeUniverse.Game.Components;
using System;

namespace StrangeUniverse.Game.Entities;

public class Asteroid
{
    public Transform   Transform { get; } = new();
    public PhysicsBody Physics   { get; } = new() { LinearDamping = 1f };
    public float       Radius    { get; set; }
    public string      TextureId { get; set; } = string.Empty;

    public Asteroid(float angle, float orbit, float radius, string textureId, Random asteroidsRng)
    {
        Radius = radius;
        TextureId = textureId;
        Transform.Position = new Vector2((float)Math.Cos(angle) * orbit, (float)Math.Sin(angle) * orbit);
        Transform.Rotation = (float)(asteroidsRng.NextDouble() * MathHelper.TwoPi);

        float speed = MathHelper.Lerp(8f, 30f, (float)asteroidsRng.NextDouble());
        float perpAngle = angle + MathHelper.PiOver2;
        Physics.Velocity = new Vector2(
            (float)Math.Cos(perpAngle) * speed,
            (float)Math.Sin(perpAngle) * speed);
        Physics.AngularVelocity = MathHelper.Lerp(-0.4f, 0.4f, (float)asteroidsRng.NextDouble());
    }
    public void Update(float deltaTime)
    {
        Physics.Integrate(Transform, deltaTime);
    }
}
