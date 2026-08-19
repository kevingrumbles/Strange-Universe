using Microsoft.Xna.Framework;

namespace Strange_Universe.Game.Entities;

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
    };
}


