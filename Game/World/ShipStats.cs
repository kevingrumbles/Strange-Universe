namespace StrangeUniverse.Game.World;

public class ShipStats
{
    public float ThrustForce    { get; set; } = 260f;
    public float RotationSpeed  { get; set; } = 3.5f;
    public float MaxSpeed       { get; set; } = 480f;
    /// <summary>1.0 = true vacuum (no passive friction). Do not set below 1.0 for ships.</summary>
    public float LinearDamping  { get; set; } = 1.0f;
    public float Radius         { get; set; } = 14f;
    /// <summary>Fraction of MaxSpeed at which the forward thrust soft-cap begins (0-1).</summary>
    public float SoftCapStart   { get; set; } = 0.75f;
}
