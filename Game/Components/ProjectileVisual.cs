using Microsoft.Xna.Framework;

namespace Strange_Universe.Game.Components;

/// <summary>
/// High-level appearance descriptor for a projectile.
/// Describes what a projectile should look like — not how to draw it.
/// <see cref="Strange_Universe.Game.Systems.ProjectileRenderer"/> turns this into graphics.
/// </summary>
public struct ProjectileVisual
{
    // -- Style -------------------------------------------------------------------

    /// <summary>Rendering pipeline used for this projectile.</summary>
    public ProjectileVisualStyle Style { get; set; }

    // -- Core bolt ---------------------------------------------------------------

    /// <summary>Colour of the bright central bolt.</summary>
    public Color CoreColor { get; set; }

    /// <summary>Length of the core bolt in world pixels.</summary>
    public int CoreLength { get; set; }

    /// <summary>Width (thickness) of the core bolt in world pixels.</summary>
    public int CoreWidth { get; set; }

    // -- Glow --------------------------------------------------------------------

    /// <summary>Colour of the soft radial glow surrounding the bolt.</summary>
    public Color GlowColor { get; set; }

    /// <summary>Radius of the glow halo in world pixels.</summary>
    public int GlowRadius { get; set; }

    /// <summary>Peak opacity of the glow (0–1).</summary>
    public float GlowIntensity { get; set; }

    // -- Trail -------------------------------------------------------------------

    /// <summary>Length of the tapered trail behind the projectile in world pixels.</summary>
    public int TrailLength { get; set; }

    /// <summary>Width of the trail at its widest (leading) edge in world pixels.</summary>
    public int TrailWidth { get; set; }

    /// <summary>Peak opacity of the trail at its leading edge (0–1).</summary>
    public float TrailAlpha { get; set; }

    // -- Animation ---------------------------------------------------------------

    /// <summary>Angular frequency of the glow/core pulse in radians per second.</summary>
    public float PulseSpeed { get; set; }

    /// <summary>Fraction of glow radius that the pulse adds/subtracts (0–1).</summary>
    public float PulseAmount { get; set; }

    // -- Particles ---------------------------------------------------------------

    /// <summary>
    /// Number of active particle slots reserved for this projectile's trail.
    /// Set to 0 to disable particles entirely.
    /// </summary>
    public int ParticleCount { get; set; }

    // -- Impact burst ------------------------------------------------------------

    /// <summary>
    /// Duration of the impact burst in seconds. Set to 0 to disable the burst,
    /// in which case the projectile despawns immediately on contact.
    /// </summary>
    public float BurstDuration { get; set; }

    /// <summary>Peak radius of the impact flash in world pixels.</summary>
    public float BurstRadius { get; set; }

    /// <summary>Number of sparks thrown outward from the impact point.</summary>
    public int BurstParticleCount { get; set; }

    /// <summary>Speed of the ejected impact sparks in world pixels per second.</summary>
    public float BurstParticleSpeed { get; set; }

    // -- Presets -----------------------------------------------------------------

    /// <summary>Default amber energy bolt — all effects enabled.</summary>
    public static ProjectileVisual Default => new ProjectileVisual
    {
        Style         = ProjectileVisualStyle.Laser,

        CoreColor     = new Color(255, 245, 200),
        CoreLength    = 22,
        CoreWidth     = 3,

        GlowColor     = new Color(255, 200, 60),
        GlowRadius    = 10,
        GlowIntensity = 0.8f,

        TrailLength   = 30,
        TrailWidth    = 5,
        TrailAlpha    = 0.45f,

        PulseSpeed    = 8f,
        PulseAmount   = 0.18f,

        ParticleCount = 12,

        BurstDuration      = 0.18f,
        BurstRadius        = 18f,
        BurstParticleCount = 10,
        BurstParticleSpeed = 120f,
    };

    /// <summary>"Light Laser" (FixedProjectile) — fast, thin cyan bolt.</summary>
    public static ProjectileVisual LightLaser => new ProjectileVisual
    {
        Style = ProjectileVisualStyle.Laser,

        CoreColor = ProjectileColorStyle.LaserCore,
        CoreLength = 22,
        CoreWidth = 3,

        GlowColor = ProjectileColorStyle.LaserGlow,
        GlowRadius = 10,
        GlowIntensity = 0.8f,

        TrailLength = 30,
        TrailWidth = 5,
        TrailAlpha = 0.45f,

        PulseSpeed = 8f,
        PulseAmount = 0.18f,

        ParticleCount = 12,

        BurstDuration      = 0.16f,
        BurstRadius        = 16f,
        BurstParticleCount = 10,
        BurstParticleSpeed = 120f,
    };

    /// <summary>"Heavy Laser" (FixedProjectile) — slower, thick crimson bolt.</summary>
    public static ProjectileVisual HeavyLaser => new ProjectileVisual
    {
        Style         = ProjectileVisualStyle.Laser,

        CoreColor     = ProjectileColorStyle.LaserCore,
        CoreLength    = 30,
        CoreWidth     = 5,

        GlowColor     = ProjectileColorStyle.LaserGlow,
        GlowRadius    = 14,
        GlowIntensity = 0.9f,

        TrailLength   = 40,
        TrailWidth    = 8,
        TrailAlpha    = 0.5f,

        PulseSpeed    = 6f,
        PulseAmount   = 0.22f,

        ParticleCount = 16,

        BurstDuration      = 0.26f,
        BurstRadius        = 30f,
        BurstParticleCount = 20,
        BurstParticleSpeed = 190f,
    };

    /// <summary>"Gatling Gun" (FixedProjectile) — small, rapid-fire tracer round.</summary>
    public static ProjectileVisual GatlingGun => new ProjectileVisual
    {
        Style         = ProjectileVisualStyle.Solid,

        CoreColor     = ProjectileColorStyle.SolidCore,
        CoreLength    = 10,
        CoreWidth     = 2,

        GlowColor     = ProjectileColorStyle.SolidGlow,
        GlowRadius    = 4,
        GlowIntensity = 0.5f,

        TrailLength   = 14,
        TrailWidth    = 2,
        TrailAlpha    = 0.25f,

        PulseSpeed    = 0f,
        PulseAmount   = 0f,

        ParticleCount = 4,

        BurstDuration      = 0.10f,
        BurstRadius        = 8f,
        BurstParticleCount = 6,
        BurstParticleSpeed = 150f,
    };

    /// <summary>"Plasma Beam" (FixedBeam) — continuous violet plasma beam.</summary>
    public static ProjectileVisual PlasmaBeam => new ProjectileVisual
    {
        Style         = ProjectileVisualStyle.Beam,

        CoreColor     = ProjectileColorStyle.PlasmaCore,
        CoreLength    = 64,
        CoreWidth     = 6,

        GlowColor     = ProjectileColorStyle.PlasmaGlow,
        GlowRadius    = 18,
        GlowIntensity = 1f,

        TrailLength   = 0,
        TrailWidth    = 0,
        TrailAlpha    = 0f,

        PulseSpeed    = 14f,
        PulseAmount   = 0.25f,

        ParticleCount = 20,

        BurstDuration      = 0.34f,
        BurstRadius        = 38f,
        BurstParticleCount = 26,
        BurstParticleSpeed = 160f,
    };

    /// <summary>"Pulse Cannon" (TurretProjectile) — rounded green energy pulse.</summary>
    public static ProjectileVisual PulseCannon => new ProjectileVisual
    {
        Style         = ProjectileVisualStyle.Pulse,

        CoreColor     = ProjectileColorStyle.PulseCore,
        CoreLength    = 14,
        CoreWidth     = 6,

        GlowColor     = ProjectileColorStyle.PulseGlow,
        GlowRadius    = 12,
        GlowIntensity = 0.85f,

        TrailLength   = 20,
        TrailWidth    = 6,
        TrailAlpha    = 0.4f,

        PulseSpeed    = 12f,
        PulseAmount   = 0.3f,

        ParticleCount = 10,

        BurstDuration      = 0.22f,
        BurstRadius        = 24f,
        BurstParticleCount = 16,
        BurstParticleSpeed = 170f,
    };

    /// <summary>"Mining Beam" (TurretBeam) — steady amber cutting beam.</summary>
    public static ProjectileVisual MiningBeam => new ProjectileVisual
    {
        Style         = ProjectileVisualStyle.Beam,

        CoreColor     = ProjectileColorStyle.MiningLaserCore,
        CoreLength    = 48,
        CoreWidth     = 3,

        GlowColor     = ProjectileColorStyle.MiningLaserGlow,
        GlowRadius    = 10,
        GlowIntensity = 0.75f,

        TrailLength   = 0,
        TrailWidth    = 0,
        TrailAlpha    = 0f,

        PulseSpeed    = 5f,
        PulseAmount   = 0.15f,

        ParticleCount = 14,

        BurstDuration      = 0.20f,
        BurstRadius        = 14f,
        BurstParticleCount = 14,
        BurstParticleSpeed = 90f,
    };
}


