namespace Strange_Universe.Game.Entities;

/// <summary>Phases of the automated jump sequence played out before a system transition.</summary>
public enum JumpPhase
{
    /// <summary>Normal flight — player has full control.</summary>
    None,
    /// <summary>Ship rotates to retrograde and brakes to a standstill.</summary>
    Decelerate,
    /// <summary>Ship rotates to face the destination system.</summary>
    Align,
    /// <summary>Ship burns hard toward the system edge with no speed cap.</summary>
    Accelerate,
    /// <summary>Ship arrives at system edge and automatically decelerates toward Mandeville Point.</summary>
    Arrival,
}
