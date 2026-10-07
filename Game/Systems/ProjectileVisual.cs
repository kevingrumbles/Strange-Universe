using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Strange_Universe.Game.Components;

namespace Strange_Universe.Game.Systems;

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

    /// <summary>Builds a visual from a JSON style definition.</summary>
    public static ProjectileVisual From(ProjectileStyleDefinition d) => new()
    {
        Style         = Enum.TryParse<ProjectileVisualStyle>(d.Style, ignoreCase: true, out var style) ? style : ProjectileVisualStyle.Laser,

        CoreColor     = ToColor(d.CoreColor),
        CoreLength    = d.CoreLength,
        CoreWidth     = d.CoreWidth,

        GlowColor     = ToColor(d.GlowColor),
        GlowRadius    = d.GlowRadius,
        GlowIntensity = d.GlowIntensity,

        TrailLength   = d.TrailLength,
        TrailWidth    = d.TrailWidth,
        TrailAlpha    = d.TrailAlpha,

        PulseSpeed    = d.PulseSpeed,
        PulseAmount   = d.PulseAmount,

        ParticleCount = d.ParticleCount,

        BurstDuration      = d.BurstDuration,
        BurstRadius        = d.BurstRadius,
        BurstParticleCount = d.BurstParticleCount,
        BurstParticleSpeed = d.BurstParticleSpeed,
    };

    private static Color ToColor(int[] rgb) =>
        rgb is { Length: >= 3 } ? new Color(rgb[0], rgb[1], rgb[2]) : Color.White;
}

/// <summary>Maps a weapon name to how its projectiles look, using the weapon and style definitions. Rendering concern, so it lives with the renderers.</summary>
public static class ProjectileVisuals
{
    // Projectiles are drawn many times a frame, so resolved visuals are cached per repository.
    private static readonly ConditionalWeakTable<DefinitionRepository, ConcurrentDictionary<string, ProjectileVisual>> Caches = new();

    public static ProjectileVisual For(string weaponName) => For(DefinitionRepository.Default, weaponName);

    public static ProjectileVisual For(DefinitionRepository repository, string weaponName)
    {
        var cache = Caches.GetValue(repository, _ => new ConcurrentDictionary<string, ProjectileVisual>(StringComparer.OrdinalIgnoreCase));
        return cache.GetOrAdd(weaponName ?? string.Empty, _ =>
        {
            var style = repository.StyleForWeapon(weaponName);
            return style == null ? ProjectileVisual.Default : ProjectileVisual.From(style);
        });
    }
}
