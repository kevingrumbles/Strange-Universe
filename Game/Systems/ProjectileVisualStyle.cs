using Microsoft.Xna.Framework;

namespace Strange_Universe.Game.Systems;

/// <summary>
/// Selects which rendering pipeline is used for a projectile.
/// Only <see cref="EnergyBolt"/> is fully implemented; the others are extension points.
/// </summary>
public enum ProjectileVisualStyle
{
    Laser,
    Plasma,
    Pulse,
    Rail,
    Solid,
    Particle,
    Beam,
}

