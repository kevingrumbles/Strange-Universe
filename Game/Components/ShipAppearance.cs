namespace Strange_Universe.Game.Components;

/// <summary>How a type of ship looks: which art it uses and how that art is drawn.</summary>
public sealed class ShipAppearance
{
    public string SpriteName { get; set; } = "Unknown";
    public string SplashName { get; set; }

    /// <summary>Added to the ship's rotation when the sprite is drawn, for art that does not point along +X.</summary>
    public float SpriteRotationOffset { get; set; } = -1.5708f;

    public float SpriteScale { get; set; } = 1.0f;
}
