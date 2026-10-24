using Microsoft.Xna.Framework;

namespace Strange_Universe.Game.Components.EquipmentComponents;

/// <summary>
/// How a weapon's projectiles look: what they should look like, not how to draw it (<c>ProjectileRenderer</c> does that).
/// Every weapon has its own (<see cref="WeaponComponent.Visual"/>), and each projectile it fires carries it. The values not set on a weapon are the default
/// amber energy bolt.
/// </summary>
public sealed class ProjectileVisual
{
    /// <summary>A separate copy with the same values.</summary>
    public ProjectileVisual Clone() => (ProjectileVisual)MemberwiseClone();

    // -- Core bolt ---------------------------------------------------------------

    /// <summary>Colour of the bright central bolt.</summary>
    public Color CoreColor { get; set; } = new(255, 245, 200);

    /// <summary>Length of the core bolt in world pixels.</summary>
    public int CoreLength { get; set; } = 22;

    /// <summary>Width (thickness) of the core bolt in world pixels.</summary>
    public int CoreWidth { get; set; } = 3;

    // -- Glow --------------------------------------------------------------------

    /// <summary>Colour of the soft radial glow surrounding the bolt.</summary>
    public Color GlowColor { get; set; } = new(255, 200, 60);

    /// <summary>Radius of the glow halo in world pixels.</summary>
    public int GlowRadius { get; set; } = 10;

    /// <summary>Peak opacity of the glow (0-1).</summary>
    public float GlowIntensity { get; set; } = 0.8f;

    // -- Trail -------------------------------------------------------------------

    /// <summary>Length of the tapered trail behind the projectile in world pixels. 0 for no trail.</summary>
    public int TrailLength { get; set; } = 30;

    /// <summary>Width of the trail at its widest (leading) edge in world pixels.</summary>
    public int TrailWidth { get; set; } = 5;

    /// <summary>Peak opacity of the trail at its leading edge (0-1).</summary>
    public float TrailAlpha { get; set; } = 0.45f;

    // -- Animation ---------------------------------------------------------------

    /// <summary>Angular frequency of the glow/core pulse in radians per second.</summary>
    public float PulseSpeed { get; set; } = 8f;

    /// <summary>Fraction of glow radius that the pulse adds/subtracts (0-1).</summary>
    public float PulseAmount { get; set; } = 0.18f;

    // -- Particles ---------------------------------------------------------------

    /// <summary>Particles a projectile sheds along its path per second. 0 for none.</summary>
    public int ParticleCount { get; set; } = 12;

    // -- Impact burst ------------------------------------------------------------

    /// <summary>
    /// Duration of the impact burst in seconds. It is also how long a spent projectile stays in the world after it hits
    /// (<see cref="WeaponComponent.ImpactLingerSeconds"/>), so the burst plays out in full. 0 disables the burst, and the
    /// projectile despawns on contact.
    /// </summary>
    public float BurstDuration { get; set; } = 0.18f;

    /// <summary>Peak radius of the impact flash in world pixels.</summary>
    public float BurstRadius { get; set; } = 18f;

    /// <summary>Number of sparks thrown outward from the impact point.</summary>
    public int BurstParticleCount { get; set; } = 10;

    /// <summary>Speed of the ejected impact sparks in world pixels per second.</summary>
    public float BurstParticleSpeed { get; set; } = 120f;
}
