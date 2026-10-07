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

public static class ProjectileColorStyle
{
    public static Color LaserCore = new Color(255, 245, 200);
    public static Color LaserGlow = new Color(255, 200, 60);
    public static Color SolidCore = new Color(255, 250, 220);
    public static Color SolidGlow = new Color(255, 170, 60);
    public static Color PlasmaCore = new Color(245, 225, 255);
    public static Color PlasmaGlow = new Color(180, 80, 255);
    public static Color PulseCore = new Color(230, 255, 230);
    public static Color PulseGlow = new Color(90, 255, 130);
    public static Color MiningLaserCore = new Color(255, 245, 215);
    public static Color MiningLaserGlow = new Color(255, 160, 40);
}
