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

    /// <summary>0 for a smooth, tapering hull, 1 for a boxy one: straight sides, flat top, stepped modules and big plates.</summary>
    public float Blockiness { get; set; }

    /// <summary>
    /// 0 for none. Otherwise the half width of a hammerhead: a broad, swept-back crossbar at the front of the ship, with glowing eyes at the tips, on a slim neck. The fuselage stops at the head.
    /// </summary>
    public float HammerSpan { get; set; }

    /// <summary>
    /// 0 for none. Otherwise a cargo ship in three parts joined by a thin spine: a bridge block at the front, this many containers (all the same size)
    /// along each side, and a block of engines at the back. The bridge is as wide as the containers on both sides together.
    /// </summary>
    public int CargoBays { get; set; }

    /// <summary>0 for the usual shape; 1 for a delta: widest at the tail and narrowing in a straight line to a point at the nose, like a flat arrowhead.</summary>
    public float Delta { get; set; }

    /// <summary>0 for none. Otherwise the half width of a rounded pod along each side of the fuselage, such as a cargo pod.</summary>
    public float SidePods { get; set; }

    /// <summary>0 for none. Otherwise a twin-boom ship: two slim hulls this far either side of the centre line, joined by a cross-piece and a central cockpit pod.</summary>
    public float TwinBoom { get; set; }

    /// <summary>0 to 2 pairs of wings; the second pair is smaller and further back. Wings do nothing in space, so keep them small.</summary>
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
