using System.Collections.Generic;
using StrangeUniverse.Game.Entities;

namespace StrangeUniverse.Game.Systems;

/// <summary>Advances physics for all asteroids each frame.</summary>
public class PhysicsSystem
{
    public void Update(IReadOnlyList<Asteroid> asteroids, float deltaTime)
    {
        foreach (var a in asteroids)
            a.Update(deltaTime);
    }
}
