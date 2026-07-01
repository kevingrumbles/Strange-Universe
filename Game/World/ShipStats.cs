namespace StrangeUniverse.Game.World;

public class ShipStats
{
    public float ThrustForce    { get; set; } = 220f;
    public float RotationSpeed  { get; set; } = 3.0f;
    public float MaxSpeed       { get; set; } = 550f;
    public float LinearDamping  { get; set; } = 0.994f;
    public float BrakeForce     { get; set; } = 260f;
    public float Radius         { get; set; } = 14f;
}
