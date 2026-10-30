using Strange_Universe.Game.Helpers;

namespace Strange_Universe.Game.Components.ShipComponents;

/// <summary>
/// How a type of ship looks. The ship's picture is generated from this (see <c>ShipHullGenerator</c>), once per type, so a type is a recipe, not a file:
/// the same values always make the same ship. Sizes are fractions of the picture, which runs from -1 to 1 with the nose toward +X.
/// </summary>
public sealed class ShipAppearance
{
    /// <summary>The picture shown on the spaceport screen (a file; the ship itself is generated).</summary>
    public string SplashName { get; set; }

    /// <summary>How large the ship is drawn compared with its radius.</summary>
    public float SpriteScale { get; set; } = 1.0f;

    /// <summary>Varies the small details (section lengths, plate tones) without changing the type's overall design.</summary>
    public int HullSeed { get; set; }

    /// <summary>Half the fuselage's widest width.</summary>
    public float BodyWidth { get; set; } = 0.22f;

    /// <summary>0 for a blunt nose, 1 for a needle.</summary>
    public float NoseSharpness { get; set; } = 0.6f;

    /// <summary>0 to 2 pairs of wings; the second pair is smaller and further back.</summary>
    public int WingPairs { get; set; } = 1;

    public float WingSpan { get; set; } = 0.7f;

    /// <summary>How far the wing tips sit behind the roots.</summary>
    public float WingSweep { get; set; } = 0.35f;

    public float WingChord { get; set; } = 0.5f;

    /// <summary>1 to 4 engines across the back. Each one gets a flame.</summary>
    public int EngineCount { get; set; } = 2;

    /// <summary>How many cabin windows along each side of the fuselage.</summary>
    public int WindowCount { get; set; } = 6;

    public Rgba HullColor { get; set; } = new(150, 155, 165);

    public Rgba AccentColor { get; set; } = new(190, 70, 50);
}
