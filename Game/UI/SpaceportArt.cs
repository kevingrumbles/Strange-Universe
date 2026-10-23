namespace Strange_Universe.Game.UI;

/// <summary>Which picture the spaceport screen shows for a planet: one image per type of planet, in the Art folder.</summary>
public static class SpaceportArt
{
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
