namespace Strange_Universe.Game.UI;

/// <summary>Which picture the spaceport screen shows for a planet: one image per type of planet, in the Art folder.</summary>
public static class SpaceportArt
{
    /// <summary>How far in from each edge of the picture the fade reaches, as a fraction of the picture's size.</summary>
    public const float FadeWidth = 0.18f;

    /// <summary>
    /// How much of the panel colour covers the picture at a point (<paramref name="x"/> and <paramref name="y"/> run from 0 to 1 across it): 1 at its
    /// edges, so the picture ends exactly in the panel colour, easing smoothly to 0 (the picture as it is) <see cref="FadeWidth"/> in from every edge.
    /// This hides the picture's own background, which is not one exact colour, so the art needs no particular background.
    /// </summary>
    public static float EdgeFade(float x, float y)
    {
        float fromEdge = System.Math.Min(System.Math.Min(x, 1f - x), System.Math.Min(y, 1f - y));
        float t = System.Math.Clamp(fromEdge / FadeWidth, 0f, 1f);
        return 1f - t * t * (3f - 2f * t); // smoothstep, so there is no visible line where the fade starts
    }

    public static string PathFor(PlanetType type) => type switch
    {
        PlanetType.Terran   => "Art/Terran_Planet_sprite.png",
        PlanetType.Rocky    => "Art/Rocky_Planet_sprite.png",
        PlanetType.GasGiant => "Art/Gas_Planet_sprite.png",
        PlanetType.Ice      => "Art/Ice_Planet_sprite.png",
        PlanetType.Lava     => "Art/Lava_Planet_sprite.png",
        PlanetType.Ocean    => "Art/Water_Planet_sprite.png",
        _                   => null,
    };
}
