namespace Strange_Universe.Game.Components.ShipComponents;

/// <summary>How a type of ship moves and how big it is: the numbers the physics, steering and collisions use.</summary>
public sealed class FlightStats
{
    public float ThrustForce   { get; set; }
    public float RotationSpeed { get; set; }
    public float MaxSpeed      { get; set; }
    public float LinearDamping { get; set; }

    /// <summary>The fraction of <see cref="MaxSpeed"/> where thrust starts to fall off towards the top speed.</summary>
    public float SoftCapStart  { get; set; }

    public float Mass          { get; set; } = 1.0f;

    /// <summary>Collision radius.</summary>
    public float Radius        { get; set; }
}
