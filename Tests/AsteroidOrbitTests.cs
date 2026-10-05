using System;
using Strange_Universe.Game.Entities;
using Xunit;

namespace Strange_Universe.Tests;

public class AsteroidOrbitTests
{
    [Theory]
    [InlineData(3000f)]
    [InlineData(9000f)]
    public void Asteroid_StaysOnOrbit_OverLongRun(float orbit)
    {
        var a = new Asteroid(angle: 0.7f, orbit: orbit, radius: 20f, textureId: "t", asteroidsRng: new Random(1));

        const float dt = 1f / 60f;
        float min = float.MaxValue, max = 0f;
        for (int i = 0; i < 60 * 60 * 30; i++) // 30 simulated minutes
        {
            a.Update(dt);
            float r = a.Position.Length();
            min = Math.Min(min, r);
            max = Math.Max(max, r);
        }

        Assert.InRange(min, orbit * 0.99f, orbit * 1.01f);
        Assert.InRange(max, orbit * 0.99f, orbit * 1.01f);
    }
}
