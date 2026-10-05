namespace Strange_Universe.Game.Systems;

/// <summary>Runtime toggles that used to be launcher fields/constants.</summary>
public sealed class GameSettings
{
    /// <summary>Draws gravity wells, hitboxes and nebula debug info.</summary>
    public bool ShowDebug { get; set; }

    /// <summary>When true, ships collide with planets (formerly <c>Launcher.PlanetColision</c>).</summary>
    public bool PlanetCollision { get; set; }
}
