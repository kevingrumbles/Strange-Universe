namespace StrangeUniverse.Game.World;

public class WorldSettings
{
    public int    Seed                    { get; set; } = 42;
    public int    PlanetCount             { get; set; } = 7;
    public int    AsteroidCount           { get; set; } = 80;
    public float  SystemRadius            { get; set; } = 18000f;
    public float  AsteroidBeltInnerRadius { get; set; } = 4500f;
    public float  AsteroidBeltOuterRadius { get; set; } = 7000f;
    public int    BackgroundStarCount     { get; set; } = 600;
    public float  StarRadius              { get; set; } = 180f;
    public float  MinPlanetRadius         { get; set; } = 40f;
    public float  MaxPlanetRadius         { get; set; } = 130f;
    public float  MinAsteroidRadius       { get; set; } = 8f;
    public float  MaxAsteroidRadius       { get; set; } = 32f;
}
